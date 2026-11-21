using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace AuthServerLab;

/// <summary>Switches the tests flip to compare configurations. The defaults are the library's own.</summary>
public sealed class LabOptions
{
    public string Issuer { get; set; } = "http://localhost/";

    /// <summary>OpenIddict encrypts access tokens (JWE) unless told not to.</summary>
    public bool EncryptAccessTokens { get; set; } = true;

    /// <summary>Opaque access tokens that are looked up in the token table.</summary>
    public bool ReferenceAccessTokens { get; set; }

    /// <summary>How long a redeemed refresh token may still be replayed without being treated as theft.</summary>
    public TimeSpan RefreshReuseLeeway { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>Where the server gets its keys. Production reads certificates from a vault; the lab generates them.</summary>
public interface IKeyProvider
{
    IReadOnlyList<X509Certificate2> SigningCertificates { get; }
    SecurityKey EncryptionKey { get; }
}

public sealed class EphemeralKeyProvider : IKeyProvider
{
    public IReadOnlyList<X509Certificate2> SigningCertificates { get; } = [CreateCertificate("CN=lab-signing", TimeSpan.FromDays(365))];

    public SecurityKey EncryptionKey { get; } = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));

    /// <summary>A throwaway self-signed signing certificate that exists only in memory.</summary>
    public static X509Certificate2 CreateCertificate(string subject, TimeSpan validFor)
    {
        var rsa = RSA.Create(2048);   // kept alive on purpose: the certificate signs with it for as long as the server runs
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow + validFor);
    }
}
