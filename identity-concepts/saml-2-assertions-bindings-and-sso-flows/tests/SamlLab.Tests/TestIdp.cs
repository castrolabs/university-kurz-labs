using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using SamlLab;

namespace SamlLab.Tests;

public sealed record ResponseSpec
{
    public string? InResponseTo { get; init; }
    public string Destination { get; init; } = TestIdp.AcsUrl;
    public string Recipient { get; init; } = TestIdp.AcsUrl;
    public string Audience { get; init; } = TestIdp.SpEntityId;
    public string Issuer { get; init; } = TestIdp.IdpEntityId;
    public string NameId { get; init; } = "alice@example.org";
    public string AssertionId { get; init; } = "_a" + Guid.NewGuid().ToString("N");
    public string StatusCode { get; init; } = SamlNs.StatusSuccess;
    public TimeSpan NotBeforeOffset { get; init; } = TimeSpan.FromSeconds(-1);
    public TimeSpan ValidFor { get; init; } = TimeSpan.FromMinutes(1);
    public bool SignAssertion { get; init; } = true;
    public bool SignResponse { get; init; }
}

/// <summary>A throwaway identity provider: a self-signed certificate that exists only in memory, plus a response builder.</summary>
public sealed class TestIdp : IDisposable
{
    public const string IdpEntityId = "http://localhost:6360/realms/demo";
    public const string SpEntityId = "http://localhost:6361/sp";
    public const string AcsUrl = "http://localhost:6361/acs";
    public const string SsoUrl = "http://localhost:6360/realms/demo/protocol/saml";

    private readonly RSA _key = RSA.Create(2048);
    public X509Certificate2 Certificate { get; }

    public TestIdp(string subject = "CN=test-idp")
    {
        var request = new CertificateRequest(subject, _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    /// <summary>The same certificate without the private key: what the SP pins from the IdP metadata.</summary>
    public X509Certificate2 PublicCertificate => X509CertificateLoader.LoadCertificate(Certificate.Export(X509ContentType.Cert));

    public XmlDocument BuildResponse(TimeProvider time, ResponseSpec spec)
    {
        var now = time.GetUtcNow();
        string F(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var responseId = "_r" + Guid.NewGuid().ToString("N");
        var inResponseTo = spec.InResponseTo is null ? "" : $" InResponseTo=\"{spec.InResponseTo}\"";
        var scdInResponseTo = inResponseTo;
        var xml = $"""
            <samlp:Response xmlns:samlp="{SamlNs.Protocol}" xmlns:saml="{SamlNs.Assertion}" ID="{responseId}" Version="2.0" IssueInstant="{F(now)}" Destination="{spec.Destination}"{inResponseTo}><saml:Issuer>{spec.Issuer}</saml:Issuer><samlp:Status><samlp:StatusCode Value="{spec.StatusCode}"/></samlp:Status><saml:Assertion ID="{spec.AssertionId}" Version="2.0" IssueInstant="{F(now)}"><saml:Issuer>{spec.Issuer}</saml:Issuer><saml:Subject><saml:NameID Format="urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress">{spec.NameId}</saml:NameID><saml:SubjectConfirmation Method="{SamlNs.BearerMethod}"><saml:SubjectConfirmationData{scdInResponseTo} NotOnOrAfter="{F(now.AddMinutes(5))}" Recipient="{spec.Recipient}"/></saml:SubjectConfirmation></saml:Subject><saml:Conditions NotBefore="{F(now + spec.NotBeforeOffset)}" NotOnOrAfter="{F(now + spec.ValidFor)}"><saml:AudienceRestriction><saml:Audience>{spec.Audience}</saml:Audience></saml:AudienceRestriction></saml:Conditions><saml:AuthnStatement AuthnInstant="{F(now)}" SessionIndex="session-1"><saml:AuthnContext><saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport</saml:AuthnContextClassRef></saml:AuthnContext></saml:AuthnStatement><saml:AttributeStatement><saml:Attribute Name="Role"><saml:AttributeValue>finance-approver</saml:AttributeValue></saml:Attribute></saml:AttributeStatement></saml:Assertion></samlp:Response>
            """;
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xml);
        var response = doc.DocumentElement!;
        var assertion = (XmlElement)response.GetElementsByTagName("Assertion", SamlNs.Assertion)[0]!;
        if (spec.SignAssertion) Sign(doc, assertion);
        if (spec.SignResponse) Sign(doc, response);
        return doc;
    }

    /// <summary>Enveloped XMLDSig, exclusive C14N, RSA-SHA256, the Reference pointing at the element's ID. The Signature goes right after the Issuer.</summary>
    private void Sign(XmlDocument doc, XmlElement element)
    {
        var signed = new SignedXml(doc) { SigningKey = _key };
        signed.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signed.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
        var reference = new Reference("#" + element.GetAttribute("ID")) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signed.AddReference(reference);
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(Certificate));
        signed.KeyInfo = keyInfo;
        signed.ComputeSignature();
        var issuer = element.ChildNodes.OfType<XmlElement>().First(e => e.LocalName == "Issuer");
        element.InsertAfter(doc.ImportNode(signed.GetXml(), true), issuer);
    }

    public void Dispose()
    {
        Certificate.Dispose();
        _key.Dispose();
    }
}

/// <summary>XML Signature Wrapping: change what is read without touching a single signed byte.</summary>
public static class Wrapping
{
    private static XmlElement Evil(XmlDocument doc, XmlElement genuine, bool keepId)
    {
        var evil = (XmlElement)genuine.CloneNode(deep: true);
        evil.RemoveChild(evil.ChildNodes.OfType<XmlElement>().First(e => e.LocalName == "Signature"));
        if (!keepId) evil.SetAttribute("ID", "_evil");
        var nameId = evil.GetElementsByTagName("NameID", SamlNs.Assertion)[0]!;
        nameId.InnerText = "admin@example.org";
        return evil;
    }

    /// <summary>Variant 1: an unsigned copy naming the admin is inserted BEFORE the genuine, signed assertion.</summary>
    public static XmlDocument CopyBefore(XmlDocument signedDoc, bool keepId = false)
    {
        var doc = (XmlDocument)signedDoc.CloneNode(deep: true);
        var genuine = (XmlElement)doc.GetElementsByTagName("Assertion", SamlNs.Assertion)[0]!;
        genuine.ParentNode!.InsertBefore(Evil(doc, genuine, keepId), genuine);
        return doc;
    }

    /// <summary>Variant 2: the unsigned admin assertion takes the genuine one's place; the genuine one is moved into its Advice element.</summary>
    public static XmlDocument HideInAdvice(XmlDocument signedDoc)
    {
        var doc = (XmlDocument)signedDoc.CloneNode(deep: true);
        var genuine = (XmlElement)doc.GetElementsByTagName("Assertion", SamlNs.Assertion)[0]!;
        var evil = Evil(doc, genuine, keepId: false);
        genuine.ParentNode!.InsertBefore(evil, genuine);
        var advice = doc.CreateElement("saml", "Advice", SamlNs.Assertion);
        evil.AppendChild(advice);
        advice.AppendChild(genuine);
        return doc;
    }
}
