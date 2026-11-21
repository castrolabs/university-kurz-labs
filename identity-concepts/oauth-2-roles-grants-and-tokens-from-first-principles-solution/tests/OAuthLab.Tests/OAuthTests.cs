using Microsoft.Extensions.Time.Testing;

namespace OAuthLab.Tests;

public sealed class OAuthTests
{
    // RFC 7636 Appendix B.
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
    private const string SpaRedirect = "http://localhost:6181/cb";

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
    private readonly AuthorizationServer _server;

    public OAuthTests() => _server = new AuthorizationServer(_clock);

    private static AuthorizeRequest AuthRequest(
        string client = "spa", string redirect = SpaRedirect, string scope = "reports.read", string responseType = "code",
        string? challenge = Challenge, string? method = "S256") =>
        new(responseType, client, redirect, scope, "state-1", challenge, method);

    private string IssueCode(string client = "spa", string redirect = SpaRedirect, string scope = "reports.read")
    {
        var response = Assert.IsType<Redirect>(_server.Authorize(AuthRequest(client, redirect, scope), "alice"));
        return response.Parameters["code"];
    }

    private TokenResponse Exchange(string code, string client = "spa", string? secret = null, string redirect = SpaRedirect, string verifier = Verifier) =>
        _server.Token(new TokenRequest("authorization_code", client, secret, Code: code, RedirectUri: redirect, CodeVerifier: verifier));

    private TokenResponse SpaLogin()
    {
        var tokens = Exchange(IssueCode());
        Assert.True(tokens.IsSuccess);
        return tokens;
    }

    private static TokenRequest WorkerCredentials(string scope = "reports.read", string secret = "worker-secret", string client = "worker") =>
        new("client_credentials", client, secret, Scope: scope);

    // --- PKCE ---

    [Fact]
    public void ShouldReproduceTheRfc7636ChallengeAndRejectBadVerifiers()
    {
        Assert.Equal(Challenge, Pkce.Challenge(Verifier));
        Assert.True(Pkce.Verify(Verifier, Challenge));
        Assert.False(Pkce.Verify(new string('a', 43), Challenge));            // a different verifier
        Assert.False(Pkce.Verify("short", Pkce.Challenge("short")));          // below 43 characters is never a valid verifier
        Assert.False(Pkce.Verify(new string('a', 129), Pkce.Challenge(new string('a', 129))));
    }

    // --- the authorization endpoint ---

    [Fact]
    public void ShouldRedirectWithCodeStateAndIssuer()
    {
        var response = Assert.IsType<Redirect>(_server.Authorize(AuthRequest(), "alice"));

        Assert.Equal(SpaRedirect, response.RedirectUri);
        Assert.False(string.IsNullOrEmpty(response.Parameters["code"]));
        Assert.Equal("state-1", response.Parameters["state"]);
        Assert.Equal(AuthorizationServer.Issuer, response.Parameters["iss"]);   // RFC 9207, the mix-up defense
    }

    [Fact]
    public void ShouldNeverRedirectToAnythingButAnExactlyRegisteredUri()
    {
        string[] attempts =
        [
            "http://localhost:6181/cb2",
            "http://localhost:6181/cb/",
            "http://localhost:6181/CB",
            "http://localhost:6181/cb?next=/evil",
            "http://localhost:6181/user-content/mallory.html",
        ];

        foreach (var redirect in attempts)
        {
            Assert.IsType<ErrorPage>(_server.Authorize(AuthRequest(redirect: redirect), "alice"));
            // "legacy" registered a wildcard: it matches only itself, never a page under the prefix.
            Assert.IsType<ErrorPage>(_server.Authorize(AuthRequest(client: "legacy", redirect: redirect), "alice"));
        }
        Assert.IsType<ErrorPage>(_server.Authorize(AuthRequest(client: "nobody"), "alice"));
    }

    [Fact]
    public void ShouldRequirePkceWithS256AndRefuseTheImplicitGrant()
    {
        var noChallenge = Assert.IsType<Redirect>(_server.Authorize(AuthRequest(challenge: null), "alice"));
        Assert.Equal("invalid_request", noChallenge.Parameters["error"]);
        Assert.Equal("state-1", noChallenge.Parameters["state"]);
        Assert.False(noChallenge.Parameters.ContainsKey("code"));

        var plain = Assert.IsType<Redirect>(_server.Authorize(AuthRequest(method: "plain"), "alice"));
        Assert.Equal("invalid_request", plain.Parameters["error"]);

        var implicitGrant = Assert.IsType<Redirect>(_server.Authorize(AuthRequest(responseType: "token"), "alice"));
        Assert.Equal("unsupported_response_type", implicitGrant.Parameters["error"]);

        var tooMuch = Assert.IsType<Redirect>(_server.Authorize(AuthRequest(scope: "reports.read reports.write"), "alice"));
        Assert.Equal("invalid_scope", tooMuch.Parameters["error"]);   // spa is only registered for reports.read
    }

    // --- authorization code and PKCE at the token endpoint ---

    [Fact]
    public void ShouldTradeTheCodeAndTheVerifierForTokensBoundToTheClient()
    {
        var tokens = SpaLogin();

        Assert.Equal("reports.read", tokens.Scope);
        Assert.NotNull(tokens.RefreshToken);
        Assert.Equal(300, tokens.ExpiresIn);

        var info = _server.Introspect(tokens.AccessToken);
        Assert.True(info.Active);
        Assert.Equal("spa", info.ClientId);
        Assert.Equal("alice", info.Subject);
        Assert.Equal(["reports-api"], info.Audience);
    }

    [Fact]
    public void ShouldBurnTheCodeOnAWrongVerifier()
    {
        var code = IssueCode();

        Assert.Equal("invalid_grant", Exchange(code, verifier: new string('x', 43)).Error);
        Assert.Equal("invalid_grant", Exchange(code).Error);   // the right verifier, but the code is gone
    }

    [Fact]
    public void ShouldRequireTheSameRedirectUriAtTheTokenEndpoint()
    {
        var code = IssueCode();

        Assert.Equal("invalid_grant", Exchange(code, redirect: "http://localhost:6181/other").Error);
    }

    [Fact]
    public void ShouldAuthenticateAConfidentialClientThatAlsoUsesPkce()
    {
        var redirect = "http://localhost:6182/cb";

        Assert.Equal("invalid_client", Exchange(IssueCode("web", redirect), "web", "wrong", redirect).Error);
        Assert.True(Exchange(IssueCode("web", redirect), "web", "web-secret", redirect).IsSuccess);
        // A code issued to one client is no good to another.
        Assert.Equal("invalid_grant", Exchange(IssueCode(), "web", "web-secret", SpaRedirect).Error);
    }

    [Fact]
    public void ShouldRevokeTheTokensAlreadyIssuedWhenACodeIsReplayed()
    {
        var code = IssueCode();
        var tokens = Exchange(code);
        Assert.True(_server.Introspect(tokens.AccessToken).Active);

        Assert.Equal("invalid_grant", Exchange(code).Error);   // someone else holds the code

        Assert.False(_server.Introspect(tokens.AccessToken).Active);
        Assert.False(_server.Introspect(tokens.RefreshToken).Active);
    }

    [Fact]
    public void ShouldExpireAnUnusedCodeAfterSixtySeconds()
    {
        var code = IssueCode();

        _clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal("invalid_grant", Exchange(code).Error);
    }

    // --- client credentials and refresh ---

    [Fact]
    public void ShouldIssueClientCredentialsTokensWithoutAUserOrARefreshToken()
    {
        var tokens = _server.Token(WorkerCredentials());

        Assert.True(tokens.IsSuccess);
        Assert.Null(tokens.RefreshToken);
        var info = _server.Introspect(tokens.AccessToken);
        Assert.Equal("service-account-worker", info.Subject);
        Assert.Equal("worker", info.ClientId);

        Assert.Equal("invalid_client", _server.Token(WorkerCredentials(secret: "nope")).Error);
        Assert.Equal(401, _server.Token(WorkerCredentials(client: "ghost")).Status);
        Assert.Equal("invalid_scope", _server.Token(WorkerCredentials(scope: "reports.delete")).Error);
        Assert.Equal("unauthorized_client", _server.Token(new TokenRequest("client_credentials", "spa")).Error);   // public client
    }

    [Fact]
    public void ShouldRotateRefreshTokensAndKillTheFamilyWhenAnOldOneIsReplayed()
    {
        var first = SpaLogin();
        var rotated = _server.Token(new TokenRequest("refresh_token", "spa", RefreshToken: first.RefreshToken));

        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
        Assert.False(_server.Introspect(first.RefreshToken).Active);

        var replay = _server.Token(new TokenRequest("refresh_token", "spa", RefreshToken: first.RefreshToken));
        Assert.Equal("invalid_grant", replay.Error);

        // The server cannot tell the thief from the user, so the newest tokens die as well.
        Assert.False(_server.Introspect(rotated.RefreshToken).Active);
        Assert.False(_server.Introspect(rotated.AccessToken).Active);
        Assert.Equal("invalid_grant", _server.Token(new TokenRequest("refresh_token", "spa", RefreshToken: rotated.RefreshToken)).Error);
    }

    [Fact]
    public void ShouldRefuseWhatOAuth21Removed()
    {
        var ropc = _server.Token(new TokenRequest("password", "spa", Username: "alice", Password: "alice-password"));
        Assert.Equal("unsupported_grant_type", ropc.Error);
        Assert.Equal(400, ropc.Status);
        Assert.Null(ropc.AccessToken);

        Assert.Equal("unsupported_grant_type", _server.Token(new TokenRequest("implicit", "spa")).Error);
        Assert.Equal("unsupported_grant_type", _server.Token(new TokenRequest(null, "spa")).Error);
    }

    // --- the resource server: scope versus audience ---

    [Fact]
    public void ShouldCheckTheAudienceAndTheScopeAndTheExpiry()
    {
        var reports = new ResourceServer(_server, "reports-api");
        var billing = new ResourceServer(_server, "billing-api");
        var token = _server.Token(WorkerCredentials(scope: "billing.read")).AccessToken;

        Assert.Equal(ResourceDecision.Allowed, billing.Check(token, "billing.read"));
        // The scope string matches, the audience does not: a token for one API is not accepted by another.
        Assert.Equal(ResourceDecision.Forbidden, reports.Check(token, "billing.read"));
        // Right audience, scope the token never had.
        var readOnly = _server.Token(WorkerCredentials(scope: "reports.read")).AccessToken;
        Assert.Equal(ResourceDecision.Forbidden, reports.Check(readOnly, "reports.write"));

        Assert.Equal(ResourceDecision.Unauthorized, reports.Check(null, "reports.read"));
        Assert.Equal(ResourceDecision.Unauthorized, reports.Check("not-a-token", "reports.read"));

        _clock.Advance(TimeSpan.FromSeconds(301));
        Assert.Equal(ResourceDecision.Unauthorized, reports.Check(readOnly, "reports.read"));
    }

    // --- optional ---

    [Fact, Trait("Optional", "true")]
    public void ShouldRevokeTokensAndAcceptRevokingGarbage()
    {
        var tokens = SpaLogin();
        var other = _server.Token(WorkerCredentials()).AccessToken;

        _server.Revoke(other);
        Assert.False(_server.Introspect(other).Active);
        Assert.True(_server.Introspect(tokens.AccessToken).Active);   // an access token dies alone

        _server.Revoke("garbage");
        _server.Revoke(null);

        _server.Revoke(tokens.RefreshToken);                          // a refresh token takes its family with it
        Assert.False(_server.Introspect(tokens.AccessToken).Active);
    }
}
