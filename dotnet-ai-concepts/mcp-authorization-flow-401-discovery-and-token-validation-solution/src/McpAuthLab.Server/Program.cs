using McpAuthLab;
using McpAuthLab.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
var authority = builder.Configuration["Server:Authority"] ?? LabUris.AuthorizationServer;
var decisions = new AuthDecisions();
builder.Services.AddSingleton(decisions);

builder.Services.AddAuthentication(o =>
    {
        o.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;   // 401 + resource_metadata
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;        // reads the bearer token
    })
    .AddJwtBearer(o =>
    {
        o.Authority = authority;          // signing keys come from {authority}/.well-known/openid-configuration
        o.MapInboundClaims = false;       // keep "scope" as "scope"
        o.TokenValidationParameters = new()
        {
            ValidIssuer = authority,
            ValidAudience = LabUris.McpEndpoint,   // the canonical URI of this endpoint, the client's `resource`
        };
        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = c =>
            {
                decisions.Add($"rejected: {c.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = c =>
            {
                var aud = string.Join(",", c.Principal!.FindAll("aud").Select(a => a.Value));
                decisions.Add($"valid token, aud={aud} scope={c.Principal.FindFirst("scope")?.Value}");
                return Task.CompletedTask;
            },
        };
    })
    .AddMcp(o => o.ResourceMetadata = new()
    {
        AuthorizationServers = { authority },
        ScopesSupported = [LabUris.Scope],
    });

builder.Services.AddAuthorization(o => o.AddPolicy("mcp", p => p
    .RequireAuthenticatedUser()
    .RequireAssertion(c => c.User.FindFirst("scope")?.Value.Split(' ').Contains(LabUris.Scope) == true)));

builder.Services.AddMcpServer().WithHttpTransport().WithTools<WhoAmITools>();

var app = builder.Build();

// The spec says a 401 SHOULD carry `scope` and a token with too few scopes gets a 403 with
// error="insufficient_scope". The SDK handler writes only resource_metadata, so fill both in on the way out.
app.Use((ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        var headers = ctx.Response.Headers;
        var metadata = $"{ctx.Request.Scheme}://{ctx.Request.Host}/.well-known/oauth-protected-resource{ctx.Request.Path}";
        if (ctx.Response.StatusCode == StatusCodes.Status401Unauthorized && headers.WWWAuthenticate.Count > 0)
        {
            headers.WWWAuthenticate = $"{headers.WWWAuthenticate}, scope=\"{LabUris.Scope}\"";
        }
        else if (ctx.Response.StatusCode == StatusCodes.Status403Forbidden)
        {
            headers.WWWAuthenticate =
                $"Bearer error=\"insufficient_scope\", scope=\"{LabUris.Scope}\", resource_metadata=\"{metadata}\"";
        }

        return Task.CompletedTask;
    });
    return next(ctx);
});

app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization("mcp");
app.Run();

public sealed class ServerMarker;
