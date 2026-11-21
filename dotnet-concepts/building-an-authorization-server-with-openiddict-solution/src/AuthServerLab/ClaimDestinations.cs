using System.Security.Claims;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServerLab;

public static class ClaimDestinations
{
    // TODO-06: Decide where each claim goes. OpenIddict copies a claim into the access token or the ID
    // token only if it has a destination: no destination, no claim, and no error. The rule for this lab:
    //   name       -> access token and ID token
    //   email      -> ID token only, and only when the "email" scope was granted
    //   role       -> access token only
    //   department -> nowhere (it must not leave the server)
    //   anything else (including sub) -> nothing here; "sub" is always included by the library.
    public static IEnumerable<string> Resolve(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Name:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;
            case Claims.Email when principal.HasScope(Scopes.Email):
                yield return Destinations.IdentityToken;
                yield break;
            case Claims.Role:
                yield return Destinations.AccessToken;
                yield break;
            default:
                yield break;
        }
    }
}
