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
    public void Rotate() => _current = NewKey();

    private static RsaSecurityKey NewKey() => new(RSA.Create(2048)) { KeyId = Guid.NewGuid().ToString("N") };
}
