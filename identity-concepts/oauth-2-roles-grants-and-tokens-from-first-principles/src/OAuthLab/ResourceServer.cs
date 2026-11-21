namespace OAuthLab;

/// <summary>An API that accepts access tokens from the authorization server.</summary>
public sealed class ResourceServer(AuthorizationServer authorizationServer, string audience)
{
    public ResourceDecision Check(string? bearerToken, string requiredScope)
    {
        // TODO-08: Introspect the token. Missing, unknown, expired or revoked: Unauthorized (401).
        //          Active but minted for another API (its audience does not contain `audience`): Forbidden (403),
        //          even when the scope string matches. Active, right audience, but without `requiredScope`: Forbidden.
        //          Otherwise Allowed.
        throw new NotImplementedException();
    }
}
