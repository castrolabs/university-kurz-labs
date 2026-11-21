using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AuthServerLab;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServerLab.Tests;

public class DiscoveryAndClientTests
{
    [Fact]
    public async Task Discovery_advertises_only_what_is_enabled_and_the_other_flows_do_not_exist()
    {
        using var factory = new ServerFactory();
        var client = factory.Browser();
        var doc = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration")).RootElement;

        string[] Strings(string name) => doc.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToArray();
        var grants = Strings("grant_types_supported");
        Assert.Contains("authorization_code", grants);
        Assert.Contains("client_credentials", grants);
        Assert.Contains("refresh_token", grants);
        Assert.DoesNotContain("password", grants);
        Assert.DoesNotContain("implicit", grants);
        Assert.Equal(["code"], Strings("response_types_supported"));
        Assert.Equal(["S256"], Strings("code_challenge_methods_supported"));   // "plain" is gone
        Assert.EndsWith("/connect/token", doc.GetProperty("token_endpoint").GetString());
        Assert.True(doc.TryGetProperty("jwks_uri", out _));

        var (_, password) = await Oauth.Post(client, "/connect/token", ("grant_type", "password"), ("client_id", "spa"), ("username", "alice"), ("password", "x"));
        Assert.Equal("unsupported_grant_type", password.GetProperty("error").GetString());

        using var implicitFlow = await client.GetAsync($"/connect/authorize?client_id=spa&redirect_uri={Uri.EscapeDataString(Oauth.Redirect)}&response_type=token&scope=orders&state=xyz");
        Assert.Equal("unsupported_response_type", await Oauth.ErrorOf(implicitFlow));
    }

    [Fact]
    public async Task Client_credentials_issue_an_encrypted_access_token_for_the_orders_api()
    {
        using var factory = new ServerFactory();
        var client = factory.Browser();
        var (status, token) = await Oauth.ClientCredentials(client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Bearer", token.GetProperty("token_type").GetString());
        Assert.InRange(token.GetProperty("expires_in").GetInt32(), 3590, 3600);   // the library's default lifetime: one hour
        var accessToken = token.GetProperty("access_token").GetString()!;

        // by default an access token is a JWE: five segments, readable only by the server (and the APIs it trusts)
        Assert.Equal(5, accessToken.Split('.').Length);
        var header = Oauth.Segment(accessToken, 0);
        Assert.Equal("A256KW", header.GetProperty("alg").GetString());
        Assert.Equal("A256CBC-HS512", header.GetProperty("enc").GetString());
        Assert.Equal("at+jwt", header.GetProperty("typ").GetString());

        // the scope's resource became the audience, and the protected API accepts the token
        var (api, claims) = await Oauth.WhoAmI(client, accessToken);
        Assert.Equal(HttpStatusCode.OK, api);
        Assert.Equal(["worker"], claims[Claims.Subject]);
        Assert.Equal(["orders-api"], claims[Claims.Audience]);
        Assert.Equal(["orders"], claims[Claims.Scope]);
    }

    [Fact]
    public async Task Permissions_are_an_allow_list_per_client()
    {
        using var factory = new ServerFactory();
        var client = factory.Browser();

        var (wrongSecret, wrong) = await Oauth.ClientCredentials(client, secret: "nope");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret);
        Assert.Equal("invalid_client", wrong.GetProperty("error").GetString());

        var (_, notPermitted) = await Oauth.ClientCredentials(client, scope: "email");        // a registered scope the worker was not given
        Assert.Equal("invalid_request", notPermitted.GetProperty("error").GetString());

        var (_, unknown) = await Oauth.ClientCredentials(client, scope: "admin");             // a scope the server does not know
        Assert.Equal("invalid_scope", unknown.GetProperty("error").GetString());

        var (_, publicClient) = await Oauth.ClientCredentials(client, clientId: "spa", secret: "");   // a public client has no secret to prove
        Assert.Equal("invalid_request", publicClient.GetProperty("error").GetString());
    }
}

public class AuthorizationCodeTests
{
    [Fact]
    public async Task Authorize_sends_an_anonymous_browser_to_the_login_page_and_comes_back()
    {
        using var factory = new ServerFactory();
        var browser = factory.Browser();
        var (_, challenge) = Oauth.Pkce();
        var url = Oauth.AuthorizeUrl(challenge);

        using var anonymous = await browser.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        var login = anonymous.Headers.Location!.OriginalString;
        Assert.StartsWith("/login", login.Replace("http://localhost", ""));
        Assert.Contains("ReturnUrl", login);                                    // the way back to this authorization request

        await Oauth.Login(browser);
        using var loggedIn = await browser.GetAsync(url);
        Assert.StartsWith(Oauth.Redirect + "?", loggedIn.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Bad_authorization_requests_are_refused_before_any_code_exists()
    {
        using var factory = new ServerFactory();
        var browser = factory.Browser();
        await Oauth.Login(browser);
        var (_, challenge) = Oauth.Pkce();

        // PKCE is mandatory, and "plain" is not accepted
        using var noChallenge = await browser.GetAsync(Oauth.AuthorizeUrl(""));
        Assert.Equal("invalid_request", await Oauth.ErrorOf(noChallenge));
        using var plain = await browser.GetAsync(Oauth.AuthorizeUrl(challenge, method: "plain"));
        Assert.NotNull(await Oauth.ErrorOf(plain));
        Assert.Null(plain.Headers.Location);                                   // no redirect, so no code

        // a scope the server does not know
        using var scope = await browser.GetAsync(Oauth.AuthorizeUrl(challenge, scope: "openid admin"));
        Assert.Equal("invalid_scope", await Oauth.ErrorOf(scope));

        // the redirect URI is matched exactly, and an unverified URI never receives a redirect
        using var slash = await browser.GetAsync(Oauth.AuthorizeUrl(challenge, redirect: Oauth.Redirect + "/"));
        Assert.Equal(HttpStatusCode.BadRequest, slash.StatusCode);
        Assert.Null(slash.Headers.Location);
    }

    [Fact]
    public async Task The_code_flow_with_pkce_returns_tokens_and_the_claims_follow_their_destinations()
    {
        using var factory = new ServerFactory();
        var browser = factory.Browser();
        // a wrong verifier does not redeem a code
        var (stolenCode, _) = await Oauth.GetCode(browser);
        var (_, wrong) = await Oauth.Redeem(browser, stolenCode, "not-the-verifier-not-the-verifier-not-the-verifier");
        Assert.Equal("invalid_grant", wrong.GetProperty("error").GetString());

        var (code, verifier) = await Oauth.GetCode(browser);
        var (status, tokens) = await Oauth.Redeem(browser, code, verifier);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(tokens.TryGetProperty("refresh_token", out _));

        // ID token: a signed JWT (three segments) with name, and email because the email scope was granted
        var idToken = tokens.GetProperty("id_token").GetString()!;
        Assert.Equal(3, idToken.Split('.').Length);
        var id = Oauth.Segment(idToken, 1);
        Assert.Equal("alice", id.GetProperty("sub").GetString());
        Assert.Equal("Alice Anderson", id.GetProperty("name").GetString());
        Assert.Equal("alice@example.org", id.GetProperty("email").GetString());
        Assert.Equal("n1", id.GetProperty("nonce").GetString());
        Assert.False(id.TryGetProperty("role", out _));
        Assert.False(id.TryGetProperty("department", out _));

        // access token: name and role, never email or department
        var (api, claims) = await Oauth.WhoAmI(browser, tokens.GetProperty("access_token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, api);
        Assert.Equal(["admin"], claims[Claims.Role]);
        Assert.Equal(["Alice Anderson"], claims[Claims.Name]);
        Assert.False(claims.ContainsKey(Claims.Email));
        Assert.False(claims.ContainsKey("department"));
        Assert.Equal(["orders-api"], claims[Claims.Audience]);
    }

    [Fact]
    public async Task Without_the_email_scope_the_id_token_has_no_email()
    {
        using var factory = new ServerFactory();
        var browser = factory.Browser();
        var (code, verifier) = await Oauth.GetCode(browser, scope: "openid profile");
        var (_, tokens) = await Oauth.Redeem(browser, code, verifier);
        var id = Oauth.Segment(tokens.GetProperty("id_token").GetString()!, 1);
        Assert.Equal("Alice Anderson", id.GetProperty("name").GetString());
        Assert.False(id.TryGetProperty("email", out _));
    }

    [Fact]
    public void Claim_destinations_follow_the_rule_and_a_missing_destination_means_a_missing_claim()
    {
        string[] Where(string type, string scope)
        {
            var identity = new ClaimsIdentity("test");
            var claim = new Claim(type, "value");
            identity.AddClaim(claim);
            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(scope.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return ClaimDestinations.Resolve(claim, principal).ToArray();
        }

        Assert.Equal([Destinations.AccessToken, Destinations.IdentityToken], Where(Claims.Name, "openid"));
        Assert.Equal([Destinations.AccessToken], Where(Claims.Role, "openid"));
        Assert.Empty(Where("department", "openid email profile"));
        Assert.Equal([Destinations.IdentityToken], Where(Claims.Email, "openid email"));
        Assert.Empty(Where(Claims.Email, "openid"));                       // not granted, not copied
    }

    [Fact]
    public async Task Redeeming_a_code_twice_revokes_everything_issued_from_it()
    {
        using var factory = new ServerFactory();
        var browser = factory.Browser();
        var (code, verifier) = await Oauth.GetCode(browser);
        var (_, tokens) = await Oauth.Redeem(browser, code, verifier);
        var accessToken = tokens.GetProperty("access_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await Oauth.WhoAmI(browser, accessToken)).Status);

        var (status, second) = await Oauth.Redeem(browser, code, verifier);          // an attacker replays the code
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("invalid_grant", second.GetProperty("error").GetString());

        // the token that was legitimately issued is revoked too, and the API sees it because validation reads the database
        Assert.Equal(HttpStatusCode.Unauthorized, (await Oauth.WhoAmI(browser, accessToken)).Status);
        var (_, refresh) = await Oauth.Refresh(browser, tokens.GetProperty("refresh_token").GetString()!);
        Assert.Equal("invalid_grant", refresh.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Refresh_tokens_rotate_and_a_reused_one_kills_the_whole_chain()
    {
        using var factory = new ServerFactory(lab: o => o.RefreshReuseLeeway = TimeSpan.Zero);
        var browser = factory.Browser();
        var (code, verifier) = await Oauth.GetCode(browser);
        var (_, first) = await Oauth.Redeem(browser, code, verifier);
        var rt0 = first.GetProperty("refresh_token").GetString()!;

        var (ok, second) = await Oauth.Refresh(browser, rt0);
        Assert.Equal(HttpStatusCode.OK, ok);
        var rt1 = second.GetProperty("refresh_token").GetString()!;
        Assert.NotEqual(rt0, rt1);                                                   // rotation: a new refresh token each time

        var (reuseStatus, reuse) = await Oauth.Refresh(browser, rt0);               // the old one is presented again
        Assert.Equal(HttpStatusCode.BadRequest, reuseStatus);
        Assert.Equal("invalid_grant", reuse.GetProperty("error").GetString());

        var (_, legit) = await Oauth.Refresh(browser, rt1);                         // theft assumed: the new one is dead as well
        Assert.Equal("invalid_grant", legit.GetProperty("error").GetString());
    }
}

public class TokenLifecycleTests
{
    [Fact]
    public async Task A_revoked_access_token_is_refused_because_validation_checks_the_database()
    {
        using var factory = new ServerFactory();
        var client = factory.Browser();
        var (_, token) = await Oauth.ClientCredentials(client);
        var accessToken = token.GetProperty("access_token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await Oauth.WhoAmI(client, accessToken)).Status);

        var (status, _) = await Oauth.Post(client, "/connect/revoke", ("client_id", "worker"), ("client_secret", "worker-secret"), ("token", accessToken));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Oauth.WhoAmI(client, accessToken)).Status);
    }

    [Fact]
    public async Task The_access_token_stops_working_when_it_expires_by_the_injected_clock()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = new ServerFactory(time: clock);
        var client = factory.Browser();
        var (_, token) = await Oauth.ClientCredentials(client);
        var accessToken = token.GetProperty("access_token").GetString()!;

        clock.Advance(TimeSpan.FromMinutes(59));
        Assert.Equal(HttpStatusCode.OK, (await Oauth.WhoAmI(client, accessToken)).Status);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Oauth.WhoAmI(client, accessToken)).Status);
    }

    [Fact]
    public async Task Token_format_switches_change_what_a_client_can_read()
    {
        // without encryption the access token is a signed JWT anybody holding it can read
        using (var plain = new ServerFactory(lab: o => o.EncryptAccessTokens = false))
        {
            var client = plain.Browser();
            var (_, token) = await Oauth.ClientCredentials(client);
            var jwt = token.GetProperty("access_token").GetString()!;
            Assert.Equal(3, jwt.Split('.').Length);
            Assert.Equal("orders-api", Oauth.Segment(jwt, 1).GetProperty("aud").GetString());
            Assert.Equal("at+jwt", Oauth.Segment(jwt, 0).GetProperty("typ").GetString());
            Assert.Equal(HttpStatusCode.OK, (await Oauth.WhoAmI(client, jwt)).Status);
        }

        // reference tokens are short opaque strings, and the payload stays in the database
        using (var reference = new ServerFactory(lab: o => o.ReferenceAccessTokens = true))
        {
            var client = reference.Browser();
            var (_, token) = await Oauth.ClientCredentials(client);
            var opaque = token.GetProperty("access_token").GetString()!;
            Assert.DoesNotContain('.', opaque);
            Assert.True(opaque.Length < 100);
            Assert.Equal(HttpStatusCode.OK, (await Oauth.WhoAmI(client, opaque)).Status);
        }
    }

    [Fact]
    public async Task Key_rotation_publishes_both_keys_and_signs_with_the_one_that_expires_last()
    {
        using var certA = EphemeralKeyProvider.CreateCertificate("CN=key-a", TimeSpan.FromDays(300));
        using var certB = EphemeralKeyProvider.CreateCertificate("CN=key-b", TimeSpan.FromDays(600));

        async Task<(string[] Kids, string TokenKid)> Run(params System.Security.Cryptography.X509Certificates.X509Certificate2[] certs)
        {
            using var factory = new ServerFactory(lab: o => o.EncryptAccessTokens = false, keys: new FixedKeys(certs));
            var client = factory.Browser();
            var jwks = JsonDocument.Parse(await client.GetStringAsync("/.well-known/jwks")).RootElement;
            var kids = jwks.GetProperty("keys").EnumerateArray().Select(k => k.GetProperty("kid").GetString()!).ToArray();
            var (_, token) = await Oauth.ClientCredentials(client);
            return (kids, Oauth.Segment(token.GetProperty("access_token").GetString()!, 0).GetProperty("kid").GetString()!);
        }

        var (kidsA, signedByA) = await Run(certA);
        Assert.Single(kidsA);
        Assert.Equal(kidsA[0], signedByA);

        var (kidsAB, signedByNew) = await Run(certA, certB);
        Assert.Equal(2, kidsAB.Length);
        Assert.Contains(kidsA[0], kidsAB);                      // the old key is still published, so old tokens still validate
        Assert.NotEqual(kidsA[0], signedByNew);                 // but new tokens use the other one

        var (_, signedByNewReversed) = await Run(certB, certA);
        Assert.Equal(signedByNew, signedByNewReversed);         // the order of registration does not matter

        var (kidsB, signedByB) = await Run(certB);              // later, the old key leaves the key set
        Assert.Single(kidsB);
        Assert.Equal(signedByNew, signedByB);
        Assert.DoesNotContain(kidsA[0], kidsB);
    }
}
