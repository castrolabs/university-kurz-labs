using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServerLab;

public static class Endpoints
{
    /// <summary>The user directory of the lab. Authenticating users is YOUR job, not OpenIddict's.</summary>
    private static readonly Dictionary<string, Claim[]> Users = new()
    {
        ["alice"] =
        [
            new(Claims.Subject, "alice"), new(Claims.Name, "Alice Anderson"), new(Claims.Email, "alice@example.org"),
            new(Claims.Role, "admin"), new("department", "finance"),
        ],
    };

    public static void MapAuthServer(this WebApplication app)
    {
        // A five-line login: the form value picks a user, no password (demo only).
        app.MapPost("/login", async (HttpContext http) =>
        {
            var form = await http.Request.ReadFormAsync();
            if (!Users.TryGetValue(form["username"].ToString(), out var claims)) return Results.Unauthorized();
            var identity = new ClaimsIdentity(claims, AuthServerSetup.CookieScheme, Claims.Name, Claims.Role);
            return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: AuthServerSetup.CookieScheme);
        }).DisableAntiforgery();
        app.MapGet("/login", () => Results.Text("login form"));

        app.MapMethods("/connect/authorize", ["GET", "POST"], Authorize);
        app.MapPost("/connect/token", Token).DisableAntiforgery();

        // A protected API, in the same app: it answers with the claims of the validated access token.
        app.MapGet("/api/whoami", (ClaimsPrincipal user) =>
            Results.Json(user.Claims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.Select(c => c.Value).ToArray())))
            .RequireAuthorization();
    }

    // TODO-07: The token endpoint (passthrough: OpenIddict has already validated the request).
    //   - client credentials: build a principal for the client (sub = the client id), copy the requested
    //     scopes, set the resources the scopes imply (scopeManager.ListResourcesAsync) and the claim
    //     destinations, and sign in with the OpenIddict server scheme;
    //   - authorization code and refresh token: OpenIddict already rebuilt the principal from the redeemed
    //     token. Authenticate with the OpenIddict server scheme and sign that principal in again (a real
    //     server would first check that the user is still allowed to sign in);
    //   - anything else: throw InvalidOperationException (it cannot happen: the flow is not enabled).
    public static async Task<IResult> Token(HttpContext http, IOpenIddictScopeManager scopeManager)
    {
        throw new NotImplementedException();
    }

    // TODO-08: The authorization endpoint. If the user has no login cookie (AuthenticateAsync with
    // AuthServerSetup.CookieScheme fails), challenge that scheme so the browser is sent to /login and
    // comes back to this very URL. Otherwise build a principal from the cookie user's claims (sub, name,
    // email, role, department), copy the requested scopes, set the resources the scopes imply and the
    // claim destinations. Then record the grant: find a valid permanent authorization of this subject, client
    // and scopes, or create one (the client's id comes from the application manager), and attach it with
    // principal.SetAuthorizationId. Without it the tokens of this flow are not linked to each other, and
    // OpenIddict cannot revoke the whole chain when a code or a refresh token is replayed. Finally sign in with
    // the OpenIddict server scheme.
    public static async Task<IResult> Authorize(
        HttpContext http, IOpenIddictApplicationManager applications, IOpenIddictAuthorizationManager authorizations,
        IOpenIddictScopeManager scopeManager)
    {
        throw new NotImplementedException();
    }
}
