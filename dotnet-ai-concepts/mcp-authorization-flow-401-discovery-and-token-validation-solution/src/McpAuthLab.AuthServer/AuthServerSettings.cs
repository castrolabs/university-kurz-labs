namespace McpAuthLab.AuthServer;

/// <summary>
/// The switches of the test server, read from configuration ("As:..."), so that each test can break
/// one thing on purpose.
/// </summary>
public sealed class AuthServerSettings
{
    public string Issuer { get; init; } = LabUris.AuthorizationServer;

    /// <summary>The "issuer" written in the metadata documents. Differs from <see cref="Issuer"/> to simulate a lying server.</summary>
    public string MetadataIssuer { get; init; } = LabUris.AuthorizationServer;

    /// <summary>When true, /.well-known/oauth-authorization-server answers 404 and only OIDC discovery works.</summary>
    public bool DisableRfc8414 { get; init; }

    /// <summary>Advertise authorization_response_iss_parameter_supported (RFC 9207).</summary>
    public bool AdvertiseIssParameter { get; init; }

    /// <summary>
    /// What the authorization response carries as "iss": "Issuer" (the issuer), "None" (no parameter) or any
    /// other value, used literally.
    /// </summary>
    public string IssParameter { get; init; } = "None";

    public static AuthServerSettings From(IConfiguration configuration) => new()
    {
        Issuer = configuration["As:Issuer"] ?? LabUris.AuthorizationServer,
        MetadataIssuer = configuration["As:MetadataIssuer"] ?? configuration["As:Issuer"] ?? LabUris.AuthorizationServer,
        DisableRfc8414 = configuration.GetValue<bool>("As:DisableRfc8414"),
        AdvertiseIssParameter = configuration.GetValue<bool>("As:AdvertiseIssParameter"),
        IssParameter = configuration["As:IssParameter"] ?? "None",
    };
}
