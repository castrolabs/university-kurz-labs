using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit.Abstractions;

namespace McpAuthLab.Tests;

public sealed class McpAuthorizationFlowTests(ITestOutputHelper output)
{
    private static CancellationToken Ct => new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

    private const string Resource = LabUris.McpEndpoint;

    private void Print(LabEnvironment lab) => output.WriteLine(lab.Wire.ToString());

    private static async Task<string> WhoAmIAsync(McpClient client)
    {
        var result = await client.CallToolAsync("whoami", cancellationToken: Ct);
        return string.Join(" ", result.Content.OfType<TextContentBlock>().Select(b => b.Text));
    }

    // --- the resource server: 401 and discovery ---

    [Fact]
    public async Task ShouldAnswer401WithAPointerToItsMetadataAndPublishThatMetadataForTheEndpointItself()
    {
        await using var lab = new LabEnvironment();
        using var http = lab.NewHttpClient(logged: false);

        var response = await lab.PostToolsListAsync(bearer: null);
        var inserted = JsonDocument.Parse(await http.GetStringAsync($"{LabUris.McpOrigin}/.well-known/oauth-protected-resource/mcp")).RootElement;
        var root = JsonDocument.Parse(await http.GetStringAsync($"{LabUris.McpOrigin}/.well-known/oauth-protected-resource")).RootElement;

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "Bearer resource_metadata=\"http://localhost:5900/.well-known/oauth-protected-resource/mcp\", scope=\"mcp:tools\"",
            string.Join(", ", response.Headers.WwwAuthenticate));
        Assert.Equal(Resource, inserted.GetProperty("resource").GetString());
        Assert.Equal(LabUris.AuthorizationServer, inserted.GetProperty("authorization_servers")[0].GetString());
        Assert.Equal("mcp:tools", inserted.GetProperty("scopes_supported")[0].GetString());
        Assert.Equal(LabUris.McpOrigin, root.GetProperty("resource").GetString());   // another resource: /mcp and the root differ
    }

    // --- the whole flow, from the wire ---

    [Fact]
    public async Task ShouldRunTheFlowInOrderAndCallTheToolWithTheToken()
    {
        await using var lab = new LabEnvironment();

        await using var client = await lab.ConnectAsync(Ct);
        var who = await WhoAmIAsync(client);
        Print(lab);

        Assert.Equal("sub=user-demo-client client_id=demo-client scope=mcp:tools", who);
        var lines = lab.Wire.Lines;
        int At(string text, int from = 0) => lines.Select((l, i) => (l, i)).First(x => x.i >= from && x.l.Contains(text)).i;
        var challenge = At("<-- 401");
        var prm = At("--> GET http://localhost:5900/.well-known/oauth-protected-resource/mcp", challenge);
        var asMetadata = At("--> GET https://localhost:5901/.well-known/oauth-authorization-server", prm);
        var authorize = At("--> GET https://localhost:5901/authorize", asMetadata);
        var token = At("--> POST https://localhost:5901/token", authorize);
        var authorized = At("[Authorization: Bearer", token);
        Assert.True(challenge < prm && prm < asMetadata && asMetadata < authorize && authorize < token && token < authorized);
        Assert.Contains("[Mcp-Method: tools/call]", lines.Last(l => l.StartsWith("--> POST http://localhost:5900/mcp")));
    }

    [Fact]
    public async Task ShouldSendPkceS256AndTheResourceInTheAuthorizationAndTheTokenRequest()
    {
        await using var lab = new LabEnvironment();

        await using var client = await lab.ConnectAsync(Ct);

        var authorize = lab.Wire.Lines.Single(l => l.StartsWith("--> GET https://localhost:5901/authorize"));
        Assert.Contains("code_challenge_method=S256", authorize);
        Assert.Contains("resource=http%3a%2f%2flocalhost%3a5900%2fmcp", authorize.ToLowerInvariant());
        Assert.Contains("scope=mcp%3atools", authorize.ToLowerInvariant());   // taken from the challenge
        var form = lab.Wire.Lines.Single(l => l.StartsWith("    form: grant_type=authorization_code"));
        Assert.Contains("code_verifier=", form);
        Assert.Contains("resource=http%3A%2F%2Flocalhost%3A5900%2Fmcp", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--> GET http://localhost:5900/mcp", string.Join('\n', lab.Wire.Lines));   // the token never goes in a URL
    }

    [Fact]
    public async Task ShouldIssueATokenForTheMcpEndpointAloneFromTheAuthorizationServer()
    {
        await using var lab = new LabEnvironment();

        var jwt = new JsonWebToken(await lab.AccessTokenAsync(Resource));

        Assert.Equal(LabUris.AuthorizationServer, jwt.Issuer);
        Assert.Equal([Resource], jwt.Audiences);
        Assert.Equal("mcp:tools", jwt.GetClaim("scope").Value);
        Assert.Equal("demo-client", jwt.GetClaim("client_id").Value);
    }

    // --- what the server accepts: 401 against 403 ---

    [Fact]
    public async Task ShouldAnswer401WithTheSameHeaderForATokenThatIsNotValidForThisServer()
    {
        await using var lab = new LabEnvironment();
        var otherServer = await lab.AccessTokenAsync("http://localhost:5910/mcp");

        var wrongAudience = await lab.PostToolsListAsync(otherServer);
        var garbage = await lab.PostToolsListAsync("abc.def.ghi");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
        Assert.Equal(wrongAudience.Headers.WwwAuthenticate.ToString(), garbage.Headers.WwwAuthenticate.ToString());
        Assert.DoesNotContain("invalid_token", wrongAudience.Headers.WwwAuthenticate.ToString());   // one challenge for every reason
        Assert.Contains(lab.Decisions.Entries, e => e.StartsWith("rejected:") && e.Contains("IDX10214"));   // audience
        Assert.Contains(lab.Decisions.Entries, e => e.StartsWith("rejected:") && e.Contains("IDX14102"));   // not a JWT
    }

    [Fact]
    public async Task ShouldAnswer403InsufficientScopeForAValidTokenWithTooFewScopes()
    {
        await using var lab = new LabEnvironment();
        var token = await lab.AccessTokenAsync(Resource, scope: "openid");

        var response = await lab.PostToolsListAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.StartsWith("Bearer error=\"insufficient_scope\"", challenge);
        Assert.Contains("scope=\"mcp:tools\"", challenge);
        Assert.Contains("resource_metadata=\"http://localhost:5900/.well-known/oauth-protected-resource/mcp\"", challenge);
        Assert.Contains("valid token, aud=http://localhost:5900/mcp scope=openid", lab.Decisions.Entries);
    }

    // --- the authorization server's half ---

    [Fact]
    public async Task ShouldRefuseAWrongCodeVerifierAndAnAuthorizationRequestWithoutResource()
    {
        await using var lab = new LabEnvironment();

        var wrongVerifier = await lab.RequestTokenAsync(Resource, verifierOnToken: new string('x', 43));
        var noResource = await lab.RequestTokenAsync(resource: null);

        Assert.Equal(HttpStatusCode.BadRequest, wrongVerifier.StatusCode);
        var error = JsonDocument.Parse(await wrongVerifier.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
        Assert.Equal("Code verifier does not match the challenge", error.GetProperty("error_description").GetString());
        Assert.Equal(HttpStatusCode.Redirect, noResource.StatusCode);   // no code: the error is in the redirect
        Assert.Contains("error=invalid_target", noResource.Headers.Location!.Query);
    }

    // --- discovery and the iss check, on the client ---

    [Fact]
    public async Task ShouldFallBackToOpenIdConfigurationWhenRfc8414MetadataIsMissing()
    {
        await using var lab = new LabEnvironment(new() { ["As:DisableRfc8414"] = "true" });

        await using var client = await lab.ConnectAsync(Ct);

        Assert.Equal("sub=user-demo-client client_id=demo-client scope=mcp:tools", await WhoAmIAsync(client));
        var lines = lab.Wire.Lines;
        var rfc8414 = lines.Select((l, i) => (l, i)).First(x => x.l.EndsWith("/.well-known/oauth-authorization-server")).i;
        Assert.StartsWith("<-- 404", lines[rfc8414 + 1]);
        Assert.Contains("--> GET https://localhost:5901/.well-known/openid-configuration", lines[rfc8414 + 2]);
    }

    [Fact]
    public async Task ShouldRefuseMetadataWhoseIssuerIsNotTheOneItWasFetchedFrom()
    {
        await using var lab = new LabEnvironment(new() { ["As:MetadataIssuer"] = "https://honest.example" });

        var ex = await Assert.ThrowsAsync<McpException>(() => lab.ConnectAsync(Ct));

        Assert.Contains("does not match the expected issuer", ex.Message + ex.InnerException?.Message);
        Assert.DoesNotContain(lab.Wire.Lines, l => l.StartsWith("--> POST https://localhost:5901/token"));   // nothing was exchanged
    }

    [Theory]
    [InlineData("true", "Issuer", null)]
    [InlineData("true", "None", "advertises RFC 9207 iss parameter support but none was received")]
    [InlineData("false", "https://evil.example", "https://evil.example' does not match expected issuer")]
    [InlineData("true", "https://localhost:5901/", "https://localhost:5901/' does not match expected issuer")]
    public async Task ShouldCompareTheIssOfTheAuthorizationResponseBeforeSendingTheCode(
        string advertise, string issParameter, string? expectedFailure)
    {
        await using var lab = new LabEnvironment(new() { ["As:AdvertiseIssParameter"] = advertise, ["As:IssParameter"] = issParameter });

        if (expectedFailure is null)
        {
            await using var client = await lab.ConnectAsync(Ct);
            Assert.StartsWith("sub=user-demo-client", await WhoAmIAsync(client));
            return;
        }

        var ex = await Assert.ThrowsAsync<McpException>(() => lab.ConnectAsync(Ct));
        Assert.Contains(expectedFailure, ex.Message + ex.InnerException?.Message);
        Assert.DoesNotContain(lab.Wire.Lines, l => l.Contains("--> POST https://localhost:5901/token"));   // the code never left the client
    }

    // --- the log itself ---

    [Fact]
    public void ShouldKeepOnlyTheStartOfLongValuesInTheWireLog()
    {
        var jwt = "eyJhbGciOiJSUzI1NiIsImtpZCI6IjEifQ.eyJpc3MiOiJodHRwczovL2xvY2FsaG9zdDo1OTAxIn0.c2lnbmF0dXJlLWJ5dGVz";

        Assert.Equal("[Authorization: Bearer eyJhbGci...]", WireRedaction.Shorten($"[Authorization: Bearer {jwt}]"));
        Assert.Equal("grant_type=authorization_code&client_id=demo-client", WireRedaction.Shorten("grant_type=authorization_code&client_id=demo-client"));
    }
}
