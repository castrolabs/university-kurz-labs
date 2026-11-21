using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace McpAuthLab.AuthServer;

/// <summary>The signing key of the test authorization server: RSA, in memory, generated at startup, discarded at exit.</summary>
public sealed class KeyRing
{
    private RsaSecurityKey _current = NewKey();

    public RsaSecurityKey Current => _current;

    public SigningCredentials Credentials => new(_current, SecurityAlgorithms.RsaSha256);

    public object Jwks()
    {
        var parameters = _current.Rsa!.ExportParameters(includePrivateParameters: false);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = _current.KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent),
                },
            },
        };
    }

    /// <summary>Replaces the signing key. The previous key is gone: a real server would publish both for a while.</summary>
    public void Rotate()
    {
        // TODO-08 (optional): Replace the current key with a new one. Then, in a scratch test, get a token,
        //          rotate, get another one and send both to the protected server: what happens to the second
        //          one, and why? (Read the Trade-offs of the lesson only afterwards.)
        throw new NotImplementedException();
    }

    private static RsaSecurityKey NewKey() => new(RSA.Create(2048)) { KeyId = Guid.NewGuid().ToString("N") };
}
