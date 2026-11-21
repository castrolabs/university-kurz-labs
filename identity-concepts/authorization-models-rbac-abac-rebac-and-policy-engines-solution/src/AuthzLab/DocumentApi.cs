namespace AuthzLab;

public sealed record ApiResult(int Status, string? Body);

/// <summary>
/// A toy API. Hiding a button is a display hint: the API must decide, on every route, for the object in the URL.
/// The token's scopes are a ceiling on what the CLIENT may ask; they are not a grant on the object.
/// </summary>
public sealed class DocumentApi(PolicyEngine engine)
{
    public const string ReadScope = "documents:read";

    /// <summary>GET /documents/{documentId} as the (already authenticated) <paramref name="userId"/>.</summary>
    public ApiResult Get(string? userId, string documentId, IReadOnlySet<string> tokenScopes)
    {
        if (userId is null || !Scenario.Users.TryGetValue(userId, out _)) return new ApiResult(401, null);
        if (!tokenScopes.Contains(ReadScope)) return new ApiResult(403, "insufficient_scope");
        if (!Scenario.Documents.TryGetValue(documentId, out var document)) return new ApiResult(404, null);
        if (!engine.Decide(Scenario.Ask(userId, "read", documentId)).Allowed) return new ApiResult(403, "forbidden");
        return new ApiResult(200, document.Get<string>("title"));
    }
}
