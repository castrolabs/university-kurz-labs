namespace OAuthLab;

/// <summary>PKCE, RFC 7636: binds an authorization code to the client instance that asked for it.</summary>
public static class Pkce
{
    public static string Challenge(string verifier)
    {
        // TODO-00: The S256 challenge is BASE64URL(SHA256(ASCII(verifier))) without padding.
        //          RFC 7636 Appendix B: verifier "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        //          gives "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM".
        throw new NotImplementedException();
    }

    public static bool Verify(string verifier, string challenge)
    {
        // TODO-00 (continued): A verifier is valid only with 43 to 128 characters from [A-Za-z0-9-._~]. Otherwise false.
        //          Then compare Challenge(verifier) with the stored challenge in constant time.
        throw new NotImplementedException();
    }
}
