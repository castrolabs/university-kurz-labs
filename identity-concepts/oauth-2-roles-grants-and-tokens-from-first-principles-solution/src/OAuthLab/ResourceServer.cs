namespace OAuthLab;

/// <summary>An API that accepts access tokens from the authorization server.</summary>
public sealed class ResourceServer(AuthorizationServer authorizationServer, string audience)
{
    public ResourceDecision Check(string? bearerToken, string requiredScope)
    {
        var token = authorizationServer.Introspect(bearerToken);
        if (!token.Active) return ResourceDecision.Unauthorized;
        if (token.Audience is null || !token.Audience.Contains(audience)) return ResourceDecision.Forbidden;
        if (!(token.Scope ?? "").Split(' ').Contains(requiredScope)) return ResourceDecision.Forbidden;
        return ResourceDecision.Allowed;
    }
}
