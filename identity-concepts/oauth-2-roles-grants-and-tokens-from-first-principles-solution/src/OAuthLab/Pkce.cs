using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace OAuthLab;

/// <summary>PKCE, RFC 7636: binds an authorization code to the client instance that asked for it.</summary>
public static class Pkce
{
    public static string Challenge(string verifier) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static bool Verify(string verifier, string challenge)
    {
        if (verifier.Length is < 43 or > 128) return false;
        if (!verifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~')) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Challenge(verifier)), Encoding.ASCII.GetBytes(challenge));
    }
}
