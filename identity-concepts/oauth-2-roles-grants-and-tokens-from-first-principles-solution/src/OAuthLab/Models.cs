namespace OAuthLab;

/// <summary>What the authorization server knows about a client. A null secret means a public client (SPA, mobile, CLI).</summary>
public sealed record ClientRegistration(
    string Id,
    string? Secret,
    IReadOnlySet<string> RedirectUris,
    IReadOnlySet<string> Grants,
    IReadOnlySet<string> Scopes)
{
    public bool IsConfidential => Secret is not null;
}

/// <summary>The query string of GET /authorize.</summary>
public sealed record AuthorizeRequest(
    string? ResponseType,
    string? ClientId,
    string? RedirectUri,
    string? Scope,
    string? State,
    string? CodeChallenge,
    string? CodeChallengeMethod);

public abstract record AuthorizeResponse;

/// <summary>The server shows this page itself. The browser is NOT redirected: the redirect URI cannot be trusted.</summary>
public sealed record ErrorPage(string Error) : AuthorizeResponse;

/// <summary>A 302 to the client's redirect URI, with these query parameters (code, state, iss or error).</summary>
public sealed record Redirect(string RedirectUri, IReadOnlyDictionary<string, string> Parameters) : AuthorizeResponse;

/// <summary>The form body of POST /token.</summary>
public sealed record TokenRequest(
    string? GrantType,
    string? ClientId = null,
    string? ClientSecret = null,
    string? Code = null,
    string? RedirectUri = null,
    string? CodeVerifier = null,
    string? RefreshToken = null,
    string? Scope = null,
    string? Username = null,
    string? Password = null);

public sealed record TokenResponse(
    int Status,
    string? Error = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? Scope = null,
    int ExpiresIn = 0)
{
    public bool IsSuccess => Status == 200;

    public static TokenResponse Failure(string error, int status = 400) => new(status, error);
}

/// <summary>RFC 7662 answer. An inactive token says nothing else.</summary>
public sealed record Introspection(
    bool Active,
    string? ClientId = null,
    string? Subject = null,
    string? Scope = null,
    IReadOnlyList<string>? Audience = null,
    long Expires = 0)
{
    public static readonly Introspection Inactive = new(false);
}

public enum ResourceDecision
{
    Allowed,
    Unauthorized,
    Forbidden,
}
