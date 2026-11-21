using McpAuthLab;
using McpAuthLab.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
var authority = builder.Configuration["Server:Authority"] ?? LabUris.AuthorizationServer;
var decisions = new AuthDecisions();
builder.Services.AddSingleton(decisions);

// TODO-00: The challenge side. Call AddAuthentication with two default schemes: the MCP scheme
//          (McpAuthenticationDefaults.AuthenticationScheme) is what answers a request with no valid token,
//          and the JWT bearer scheme is what reads the Authorization header. Then add the MCP scheme with
//          AddMcp: its ResourceMetadata names the authorization server (`authority`) and the scope this
//          server needs (LabUris.Scope). It produces the 401 pointer and /.well-known/oauth-protected-resource.
//
// TODO-01: The validation side. Add JwtBearer: Authority = authority (the signing keys come from its OIDC
//          discovery), MapInboundClaims = false (so the claim stays "scope"), and TokenValidationParameters
//          with ValidIssuer = authority and ValidAudience = LabUris.McpEndpoint. In JwtBearerEvents, write to
//          `decisions` why a token was rejected ("rejected: <exception message>", OnAuthenticationFailed) and
//          what an accepted one carries ("valid token, aud=<audiences> scope=<scope>", OnTokenValidated).
builder.Services.AddAuthentication();

// TODO-02 (part 1): A policy named "mcp": an authenticated user whose `scope` claim, split on spaces,
//          contains LabUris.Scope. A valid token without the scope must fail authorization, not authentication.
builder.Services.AddAuthorization();

builder.Services.AddMcpServer().WithHttpTransport().WithTools<WhoAmITools>();

var app = builder.Build();

// TODO-03: The spec says a 401 SHOULD carry `scope`, and a token with too few scopes gets a 403 whose
//          WWW-Authenticate is Bearer error="insufficient_scope", scope="mcp:tools" and a resource_metadata
//          pointer. The SDK handler writes only resource_metadata. Add a middleware here, before
//          UseAuthentication, that fills both in when the response starts (Response.OnStarting): append
//          scope="..." to the existing challenge of a 401, and replace the header of a 403. The metadata URL
//          is {scheme}://{host}/.well-known/oauth-protected-resource{path}.

app.UseAuthentication();
app.UseAuthorization();

// TODO-02 (part 2): Require that policy on the MCP endpoint.
app.MapMcp("/mcp");
app.Run();

public sealed class ServerMarker;
