using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServerLab;

public static class AuthServerSetup
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    public static IServiceCollection AddAuthServer(this IServiceCollection services)
    {
        services.AddOptions<LabOptions>();
        services.TryAddSingleton<IKeyProvider, EphemeralKeyProvider>();   // a test can register its own provider

        // One in-memory SQLite database per host. The connection stays open for the life of the host (the
        // database disappears when it closes). EF Core's UseInMemoryDatabase would not do: OpenIddict
        // revokes a whole chain of tokens with a bulk update, which that provider does not support.
        services.AddSingleton(_ =>
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            return connection;
        });
        services.AddDbContext<DbContext>((sp, o) =>
        {
            o.UseSqlite(sp.GetRequiredService<SqliteConnection>());
            o.UseOpenIddict();                                              // adds the four OpenIddict tables to the model
        });

        services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .AddCookie(CookieScheme, o => o.LoginPath = "/login");
        services.AddAuthorization();

        services.AddOpenIddict()
            .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<DbContext>())
            .AddServer(o =>
            {
                ConfigureEndpointsAndFlows(o);
                ConfigurePkce(o);
            })
            .AddValidation(ConfigureValidation);

        // Options that depend on other services (the key provider, the lab switches) are set here.
        services.AddOptions<OpenIddictServerOptions>().Configure<IOptions<LabOptions>, IKeyProvider>((options, lab, keys) =>
        {
            options.Issuer = new Uri(lab.Value.Issuer);
            ConfigureKeys(options, keys);
            ConfigureTokenFormat(options, lab.Value);
        });

        services.AddHostedService<SeedWorker>();
        return services;
    }

    // TODO-00: Endpoints and flows. Token endpoint "connect/token", authorization endpoint
    // "connect/authorize", revocation endpoint "connect/revoke". Allow ONLY the authorization code,
    // client credentials and refresh token flows (no password, no implicit). Register the scopes the server
    // knows: email, profile, offline_access and "orders". Turn on the ASP.NET Core integration with
    // passthrough for the authorization and token endpoints (your handlers will issue the tokens), and
    // disable the transport security requirement: the lab runs on plain HTTP.
    public static void ConfigureEndpointsAndFlows(OpenIddictServerBuilder o)
    {
        o.SetAuthorizationEndpointUris("connect/authorize")
         .SetTokenEndpointUris("connect/token")
         .SetRevocationEndpointUris("connect/revoke");
        o.AllowAuthorizationCodeFlow().AllowClientCredentialsFlow().AllowRefreshTokenFlow();
        o.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, "orders");
        o.UseAspNetCore()
         .EnableAuthorizationEndpointPassthrough()
         .EnableTokenEndpointPassthrough()
         .DisableTransportSecurityRequirement();
    }

    // TODO-01: PKCE. Require a code challenge on every authorization request, and make S256 the only
    // method: by default the discovery document also lists "plain", and the server accepts it.
    public static void ConfigurePkce(OpenIddictServerBuilder o)
    {
        o.RequireProofKeyForCodeExchange();
        o.Configure(options => options.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
    }

    // TODO-02: Keys. Register EVERY certificate of the provider as a signing credential (so that an old
    // and a new key can be published together during a rotation) and the provider's encryption key.
    // Do not choose which one signs: the server prefers the certificate that expires last.
    public static void ConfigureKeys(OpenIddictServerOptions options, IKeyProvider keys)
    {
        foreach (var certificate in keys.SigningCertificates)
            options.SigningCredentials.Add(new SigningCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaSha256));
        options.EncryptionCredentials.Add(new EncryptingCredentials(keys.EncryptionKey, SecurityAlgorithms.Aes256KW, SecurityAlgorithms.Aes256CbcHmacSha512));
    }

    // TODO-03: Token format and refresh rotation, driven by the LabOptions switches: do not encrypt the
    // access tokens when EncryptAccessTokens is false, use reference access tokens when
    // ReferenceAccessTokens is true, and apply RefreshReuseLeeway as the refresh token reuse leeway.
    // TODO-09 (optional): also apply lab.AccessTokenLifetime as the access token lifetime, then write a
    // test that reads expires_in from the token response.
    public static void ConfigureTokenFormat(OpenIddictServerOptions options, LabOptions lab)
    {
        options.DisableAccessTokenEncryption = !lab.EncryptAccessTokens;
        options.UseReferenceAccessTokens = lab.ReferenceAccessTokens;
        options.RefreshTokenReuseLeeway = lab.RefreshReuseLeeway;
        options.AccessTokenLifetime = lab.AccessTokenLifetime;
    }

    // TODO-04: Validation of the tokens this server issues (it is also the API in the lab). Use the local
    // server, the ASP.NET Core integration, and make validation look the token (and its authorization)
    // up in the database: without that a revoked access token is still accepted until it expires.
    public static void ConfigureValidation(OpenIddictValidationBuilder o)
    {
        o.UseLocalServer();
        o.UseAspNetCore();
        o.EnableTokenEntryValidation();
        o.EnableAuthorizationEntryValidation();
    }
}
