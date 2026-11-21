using System.Security;
using System.Security.Cryptography;
using System.Xml;

namespace SamlLab;

/// <summary>The service provider (SP) side of SAML Web Browser SSO, without any web framework.</summary>
public sealed class SamlServiceProvider(SpOptions options, TimeProvider time)
{
    public PendingRequests Pending { get; } = new();
    public ReplayCache Replays { get; } = new(time);

    /// <summary>SP-initiated step 1: an AuthnRequest with a fresh random ID, remembered until the IdP answers.</summary>
    public (string Id, string Xml) CreateAuthnRequest(string idpSsoUrl)
    {
        var id = "_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Pending.Add(id);
        var instant = time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        var xml = $"""
            <samlp:AuthnRequest xmlns:samlp="{SamlNs.Protocol}" xmlns:saml="{SamlNs.Assertion}" ID="{id}" Version="2.0" IssueInstant="{instant}" ProtocolBinding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST" Destination="{SecurityElement.Escape(idpSsoUrl)}" AssertionConsumerServiceURL="{SecurityElement.Escape(options.AcsUrl)}"><saml:Issuer>{SecurityElement.Escape(options.EntityId)}</saml:Issuer></samlp:AuthnRequest>
            """;
        return (id, xml);
    }

    /// <summary>The browser redirect to the IdP (HTTP-Redirect binding): deflated, base64 and URL-encoded.</summary>
    public (string Id, string Url) BuildRedirect(string idpSsoUrl, string relayState)
    {
        var (id, xml) = CreateAuthnRequest(idpSsoUrl);
        var url = $"{idpSsoUrl}?SAMLRequest={Uri.EscapeDataString(SamlBindings.EncodeRedirect(xml))}&RelayState={Uri.EscapeDataString(relayState)}";
        return (id, url);
    }

    // TODO-07: Process the SAMLResponse form field of an HTTP-POST binding (base64 of the XML). Throw
    // SamlValidationException for anything wrong, in this order: decode and parse safely
    // (SamlXml.Load), get the signed assertion (AssertionVerifier), then, reading ONLY from that
    // assertion element: Issuer equals options.IdpEntityId, time window, audience, bearer
    // confirmation (SubjectChecks), and finally the replay cache (an ID seen before is a replay;
    // remember it until its NotOnOrAfter plus the skew). Also require the Response's status code to be
    // Success. Return the NameID, the AuthnStatement's SessionIndex and the attributes
    // (AttributeReader.ReadFirstValues is provided).
    public SamlPrincipal ProcessPostResponse(string samlResponse)
    {
        throw new NotImplementedException();
    }
}

public static class AttributeReader
{
    /// <summary>The first AttributeValue of each Attribute, the last Attribute of a name wins. Simple, and lossy.</summary>
    public static IReadOnlyDictionary<string, string[]> ReadFirstValues(XmlElement assertion)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var statement = SamlXml.Child(assertion, SamlNs.Assertion, "AttributeStatement");
        if (statement is null) return result;
        foreach (var attribute in SamlXml.Children(statement, SamlNs.Assertion, "Attribute"))
            result[attribute.GetAttribute("Name")] =
                SamlXml.Child(attribute, SamlNs.Assertion, "AttributeValue") is { } v ? [v.InnerText.Trim()] : [];
        return result;
    }

    // TODO-08 (optional): Read the AttributeStatement into name -> values. An IdP may emit one Attribute
    // with many AttributeValue elements, OR the same Attribute Name repeated with one value each (the
    // lesson's role mapper "single" toggle): a reader that keeps one of them silently drops roles.
    // Merge both shapes, then call it from ProcessPostResponse instead of ReadFirstValues.
    public static IReadOnlyDictionary<string, string[]> ReadMultiValued(XmlElement assertion)
    {
        throw new NotImplementedException();
    }
}
