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
        // TODO-08: Answer, in this order:
        //          - 401 when there is no such user (userId null or not in Scenario.Users);
        //          - 403 when the token lacks the `documents:read` scope (the ceiling);
        //          - 404 when the document does not exist in Scenario.Documents;
        //          - 403 when the policy engine does not allow "read" for this user on THIS document (the object check:
        //            alice must not read dave's document by changing the id in the URL, scope or not);
        //          - otherwise 200 with the document title as the body.
        throw new NotImplementedException();
    }
}
