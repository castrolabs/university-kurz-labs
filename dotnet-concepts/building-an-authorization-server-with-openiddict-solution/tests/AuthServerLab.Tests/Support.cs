using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthServerLab;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AuthServerLab.Tests;

/// <summary>The authorization server in memory (TestServer): no port is opened.</summary>
public sealed class ServerFactory(Action<LabOptions>? lab = null, IKeyProvider? keys = null, TimeProvider? time = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            if (lab is not null) services.Configure(lab);
            if (keys is not null) services.AddSingleton(keys);
            if (time is not null) services.AddSingleton(time);
        });

    /// <summary>A browser-like client: it keeps cookies and never follows a redirect on its own.</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
}

public sealed class FixedKeys(params System.Security.Cryptography.X509Certificates.X509Certificate2[] certificates) : IKeyProvider
{
    public IReadOnlyList<System.Security.Cryptography.X509Certificates.X509Certificate2> SigningCertificates { get; } = certificates;
    public Microsoft.IdentityModel.Tokens.SecurityKey EncryptionKey { get; } = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
}

public static class Oauth
{
    public const string Redirect = Seed.SpaRedirectUri;
    public const string AllScopes = "openid email profile offline_access orders";

    public static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    public static JsonElement Segment(string jwt, int index) => JsonDocument.Parse(FromBase64Url(jwt.Split('.')[index])).RootElement;

    public static async Task<(HttpStatusCode Status, JsonElement Json)> Post(HttpClient client, string url, params (string, string)[] form)
    {
        using var response = await client.PostAsync(url, new FormUrlEncodedContent(form.Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2))));
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    public static Task<(HttpStatusCode Status, JsonElement Json)> ClientCredentials(HttpClient client, string scope = "orders", string secret = "worker-secret", string clientId = "worker") =>
        Post(client, "/connect/token", ("grant_type", "client_credentials"), ("client_id", clientId), ("client_secret", secret), ("scope", scope));

    public static async Task Login(HttpClient browser) =>
        Assert.Equal(HttpStatusCode.OK, (await Post(browser, "/login", ("username", "alice"))).Status);   // sets the login cookie

    public static string AuthorizeUrl(string challenge, string scope = AllScopes, string method = "S256", string redirect = Redirect, string? state = "xyz", string nonce = "n1") =>
        $"/connect/authorize?client_id=spa&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&scope={Uri.EscapeDataString(scope)}" +
        $"&state={state}&nonce={nonce}" + (challenge.Length > 0 ? $"&code_challenge={challenge}&code_challenge_method={method}" : "");

    public static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p.Length > 1 ? p[1] : ""));

    /// <summary>Logs in, runs the authorization request and returns the authorization code.</summary>
    public static async Task<(string Code, string Verifier)> GetCode(HttpClient browser, string scope = AllScopes)
    {
        await Login(browser);
        var (verifier, challenge) = Pkce();
        using var response = await browser.GetAsync(AuthorizeUrl(challenge, scope));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = Query(response.Headers.Location!);
        Assert.Equal("xyz", query["state"]);
        return (query["code"], verifier);
    }

    public static Task<(HttpStatusCode Status, JsonElement Json)> Redeem(HttpClient client, string code, string verifier) =>
        Post(client, "/connect/token", ("grant_type", "authorization_code"), ("client_id", "spa"), ("code", code),
            ("redirect_uri", Redirect), ("code_verifier", verifier));

    public static Task<(HttpStatusCode Status, JsonElement Json)> Refresh(HttpClient client, string refreshToken) =>
        Post(client, "/connect/token", ("grant_type", "refresh_token"), ("client_id", "spa"), ("refresh_token", refreshToken));

    /// <summary>Calls the protected API with a bearer token and returns the status and the claims the API saw.</summary>
    public static async Task<(HttpStatusCode Status, Dictionary<string, string[]> Claims)> WhoAmI(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/whoami");
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, response.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<Dictionary<string, string[]>>(body)!
            : []);
    }

    /// <summary>The OAuth error code of a response: from a JSON body, or from the query of a redirect to the client.</summary>
    public static async Task<string?> ErrorOf(HttpResponseMessage response)
    {
        if (response.Headers.Location is { } location) return Query(location).GetValueOrDefault("error");
        var text = await response.Content.ReadAsStringAsync();
        if (text.StartsWith('{')) return JsonDocument.Parse(text).RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        // the authorization endpoint answers an invalid request with a 400 and plain "error:..." lines
        return text.Split('\n').FirstOrDefault(l => l.StartsWith("error:"))?["error:".Length..].Trim();
    }
}
