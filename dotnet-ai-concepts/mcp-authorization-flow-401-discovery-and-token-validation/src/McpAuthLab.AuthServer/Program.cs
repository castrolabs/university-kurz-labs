using System.Security.Cryptography;
using System.Text;
using McpAuthLab;
using McpAuthLab.AuthServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(AuthServerSettings.From(builder.Configuration));
builder.Services.AddSingleton<KeyRing>();
builder.Services.AddSingleton<AuthorizationCodes>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Test-only authorization server: one pre-registered client, no login, no consent, any scope granted.
// It is a stand-in for the real thing so that the MCP server and client have somebody to talk to.

static object Metadata(AuthServerSettings s) => new Dictionary<string, object>
{
    ["issuer"] = s.MetadataIssuer,
    ["authorization_endpoint"] = $"{s.Issuer}/authorize",
    ["token_endpoint"] = $"{s.Issuer}/token",
    ["jwks_uri"] = $"{s.Issuer}/jwks",
    ["response_types_supported"] = new[] { "code" },
    ["grant_types_supported"] = new[] { "authorization_code" },
    ["code_challenge_methods_supported"] = new[] { "S256" },
    ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post" },
    ["scopes_supported"] = new[] { LabUris.Scope },
    ["authorization_response_iss_parameter_supported"] = s.AdvertiseIssParameter,
};

app.MapGet("/.well-known/oauth-authorization-server", (AuthServerSettings s) =>
    s.DisableRfc8414 ? Results.NotFound() : Results.Json(Metadata(s)));

app.MapGet("/.well-known/openid-configuration", (AuthServerSettings s) => Results.Json(Metadata(s)));

app.MapGet("/jwks", (KeyRing keys) => Results.Json(keys.Jwks()));

app.MapGet("/authorize", (HttpContext ctx, AuthorizationCodes codes, AuthServerSettings s) =>
{
    var q = ctx.Request.Query;
    if (q["client_id"] != LabUris.ClientId || q["redirect_uri"] != LabUris.ClientCallback)
    {
        return Results.BadRequest(new { error = "invalid_request", error_description = "Unknown client or redirect_uri." });
    }

    static IResult Back(string url, string error, string description, string? state) =>
        Results.Redirect($"{url}?error={error}&error_description={Uri.EscapeDataString(description)}"
                         + (state is null ? "" : $"&state={Uri.EscapeDataString(state)}"));

    string? state = q["state"];
    if (q["response_type"] != "code" || q["code_challenge_method"] != "S256" || string.IsNullOrEmpty(q["code_challenge"]))
    {
        return Back(LabUris.ClientCallback, "invalid_request", "response_type=code and a S256 code_challenge are required.", state);
    }

    string? resource = q["resource"];
    // TODO-05: RFC 8707, part 1. A request with no `resource` is refused: redirect back to the client with
    //          error=invalid_target (the Back helper above builds the redirect). The token request repeats this
    //          check in part 2, below.
    resource ??= "";

    var code = codes.Issue(new IssuedCode(LabUris.ClientId, LabUris.ClientCallback, q["code_challenge"]!, resource, q["scope"].ToString()));

    var location = $"{LabUris.ClientCallback}?code={code}" + (state is null ? "" : $"&state={Uri.EscapeDataString(state)}");
    // TODO-06: RFC 9207. When s.IssParameter is not "None", add `iss` to the query of the redirect:
    //          the issuer itself when it is "Issuer", otherwise the configured text, literally.
    return Results.Redirect(location);
});

app.MapPost("/token", async (HttpContext ctx, AuthorizationCodes codes, KeyRing keys, AuthServerSettings s, TimeProvider clock) =>
{
    var form = await ctx.Request.ReadFormAsync();
    static IResult Error(string error, string description) =>
        Results.Json(new { error, error_description = description }, statusCode: StatusCodes.Status400BadRequest);

    if (form["grant_type"] != "authorization_code")
    {
        return Error("unsupported_grant_type", "Only authorization_code is supported.");
    }

    if (form["client_id"] != LabUris.ClientId || form["client_secret"] != LabUris.ClientSecret)
    {
        return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var issued = codes.Redeem(form["code"].ToString());
    if (issued is null || issued.RedirectUri != form["redirect_uri"])
    {
        return Error("invalid_grant", "Unknown, used or mismatched authorization code.");
    }

    // TODO-04: PKCE. Base64Url(SHA-256(code_verifier)) must equal issued.CodeChallenge. When it does not,
    //          answer Error("invalid_grant", "Code verifier does not match the challenge").

    // TODO-05: RFC 8707, part 2. The `resource` of the token request must be the one given at /authorize,
    //          otherwise Error("invalid_target", ...). The token is for that resource and nobody else: it goes
    //          into `aud` (the variable below already does that).
    var audience = issued.Resource;

    var now = clock.GetUtcNow();
    var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = s.Issuer,
        Audience = audience,
        IssuedAt = now.UtcDateTime,
        NotBefore = now.UtcDateTime,
        Expires = now.AddHours(1).UtcDateTime,
        SigningCredentials = keys.Credentials,
        Claims = new Dictionary<string, object>
        {
            ["sub"] = $"user-{issued.ClientId}",
            ["client_id"] = issued.ClientId,
            ["scope"] = issued.Scope,
            ["jti"] = Guid.NewGuid().ToString(),
        },
    });

    return Results.Json(new { access_token = token, token_type = "Bearer", expires_in = 3600, scope = issued.Scope });
});

app.Run();

public sealed class AuthServerMarker;
