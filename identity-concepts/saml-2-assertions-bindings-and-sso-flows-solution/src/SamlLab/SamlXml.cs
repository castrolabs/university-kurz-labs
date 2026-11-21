using System.Xml;

namespace SamlLab;

public sealed class SamlValidationException(string message) : Exception(message);

public static class SamlNs
{
    public const string Assertion = "urn:oasis:names:tc:SAML:2.0:assertion";
    public const string Protocol = "urn:oasis:names:tc:SAML:2.0:protocol";
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";
    public const string StatusSuccess = "urn:oasis:names:tc:SAML:2.0:status:Success";
    public const string BearerMethod = "urn:oasis:names:tc:SAML:2.0:cm:bearer";
}

public static class SamlXml
{
    // TODO-01: Parse untrusted XML safely. Keep the whitespace (a signature is computed over the exact
    // bytes), forbid DTDs completely (no entity expansion, no external entities) and never resolve
    // anything. Malformed or forbidden input must surface as SamlValidationException, not XmlException.
    public static XmlDocument Load(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            doc.Load(reader);
        }
        catch (XmlException e)
        {
            throw new SamlValidationException($"Malformed or forbidden XML: {e.Message}");
        }
        return doc;
    }

    /// <summary>Direct children only, never a search of the whole document: that is how wrapping attacks win.</summary>
    public static IEnumerable<XmlElement> Children(XmlElement parent, string ns, string localName) =>
        parent.ChildNodes.OfType<XmlElement>().Where(e => e.NamespaceURI == ns && e.LocalName == localName);

    public static XmlElement? Child(XmlElement parent, string ns, string localName) =>
        Children(parent, ns, localName).FirstOrDefault();

    public static DateTimeOffset ParseInstant(string value) => XmlConvert.ToDateTimeOffset(value);
}
