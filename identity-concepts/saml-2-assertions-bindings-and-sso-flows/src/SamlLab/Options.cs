using System.Security.Cryptography.X509Certificates;

namespace SamlLab;

/// <summary>What this service provider knows from the IdP's metadata and from its own configuration.</summary>
/// <param name="EntityId">This SP's entityID: the audience an assertion must name and the Issuer of its requests.</param>
/// <param name="AcsUrl">This SP's AssertionConsumerService URL: the only place a response may be delivered to.</param>
/// <param name="IdpEntityId">The IdP's entityID: the only acceptable Issuer.</param>
/// <param name="IdpCertificate">The IdP's signing certificate, pinned from its metadata. Nothing else is trusted.</param>
public sealed record SpOptions(string EntityId, string AcsUrl, string IdpEntityId, X509Certificate2 IdpCertificate)
{
    /// <summary>Tolerated difference between the IdP's clock and ours.</summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Accept an unsolicited (IdP-initiated) response that has no InResponseTo. Off by default.</summary>
    public bool AllowIdpInitiated { get; init; }
}

public sealed record SamlPrincipal(string NameId, string? SessionIndex, IReadOnlyDictionary<string, string[]> Attributes);
