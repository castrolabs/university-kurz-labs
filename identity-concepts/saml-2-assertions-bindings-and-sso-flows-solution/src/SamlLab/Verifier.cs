using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace SamlLab;

/// <summary>
/// The bug from the lesson, written out. It checks that SOME signature in the document is valid and
/// then reads the first NameID it finds. A valid signature over one element says nothing about the
/// element that gets read.
/// </summary>
public static class NaiveVerifier
{
    public static string ReadNameId(XmlDocument doc, X509Certificate2 idpCertificate)
    {
        var signature = (XmlElement?)doc.GetElementsByTagName("Signature", SamlNs.Ds)[0]
            ?? throw new SamlValidationException("No signature");
        var signed = new SignedXml(doc);
        signed.LoadXml(signature);
        if (!signed.CheckSignature(idpCertificate, verifySignatureOnly: true))
            throw new SamlValidationException("Invalid document signature");
        return doc.GetElementsByTagName("NameID", SamlNs.Assertion)[0]!.InnerText;   // first match anywhere
    }
}

public static class AssertionVerifier
{
    // TODO-02: Return the Assertion element that the pinned IdP certificate really signed, or throw
    // SamlValidationException. The cryptography is not enough, so also check position and identity:
    //   - the Response contains exactly ONE Assertion as a DIRECT child (not nested anywhere else);
    //   - that Assertion carries its OWN Signature as a direct child (a signature only on the
    //     Response does not protect the assertion);
    //   - the Signature has exactly one Reference and its URI is "#" plus THAT assertion's ID;
    //   - no other element in the document uses that ID (SignedXml resolves a reference by ID
    //     anywhere in the document);
    //   - SignedXml.CheckSignature(certificate, verifySignatureOnly: true) succeeds. A CryptographicException
    //     is a rejection too.
    // Everything the caller reads afterwards must come from the returned element only.
    public static XmlElement GetSignedAssertion(XmlDocument doc, X509Certificate2 idpCertificate)
    {
        var response = doc.DocumentElement;
        if (response is null || response.NamespaceURI != SamlNs.Protocol || response.LocalName != "Response")
            throw new SamlValidationException("Root element is not a SAML Response");

        var assertions = SamlXml.Children(response, SamlNs.Assertion, "Assertion").ToList();
        if (assertions.Count != 1)
            throw new SamlValidationException($"Expected exactly one Assertion child of the Response, found {assertions.Count}");
        var assertion = assertions[0];

        var signatures = SamlXml.Children(assertion, SamlNs.Ds, "Signature").ToList();
        if (signatures.Count != 1) throw new SamlValidationException("The assertion itself is not signed");

        var id = assertion.GetAttribute("ID");
        if (id.Length == 0) throw new SamlValidationException("The assertion has no ID");
        var sameId = doc.GetElementsByTagName("*").Cast<XmlElement>()
            .Count(e => e.GetAttribute("ID") == id || e.GetAttribute("Id") == id || e.GetAttribute("id") == id);
        if (sameId != 1) throw new SamlValidationException($"The ID '{id}' is used by {sameId} elements");

        try
        {
            var signed = new SignedXml(doc);
            signed.LoadXml(signatures[0]);
            if (signed.SignedInfo!.References.Count != 1 || ((Reference)signed.SignedInfo.References[0]!).Uri != "#" + id)
                throw new SamlValidationException("The signature does not reference this assertion");
            if (!signed.CheckSignature(idpCertificate, verifySignatureOnly: true))
                throw new SamlValidationException("Invalid document signature");
        }
        catch (CryptographicException e)
        {
            throw new SamlValidationException($"Invalid document signature: {e.Message}");
        }
        return assertion;
    }
}
