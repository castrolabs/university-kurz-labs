using System.IO.Compression;
using System.Text;

namespace SamlLab;

/// <summary>How a SAML message travels through the browser (Bindings spec, section 3.4 and 3.5).</summary>
public static class SamlBindings
{
    // TODO-00: HTTP-Redirect binding: the XML is compressed with raw DEFLATE (no zlib header, no gzip
    // header), then base64 encoded. Decode is the exact inverse. The caller URL-encodes the result.
    public static string EncodeRedirect(string xml)
    {
        throw new NotImplementedException();
    }

    // TODO-00 (second half): DecodeRedirect, the exact inverse of EncodeRedirect.
    public static string DecodeRedirect(string base64)
    {
        throw new NotImplementedException();
    }

    /// <summary>HTTP-POST binding: base64 only, no compression.</summary>
    public static string EncodePost(string xml) => Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));

    public static string DecodePost(string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}
