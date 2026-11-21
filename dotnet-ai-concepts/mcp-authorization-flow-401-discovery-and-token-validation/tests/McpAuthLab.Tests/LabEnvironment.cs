using System.Security.Cryptography;
using System.Text;
using System.Web;
using McpAuthLab.AuthServer;
using McpAuthLab.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Client;

namespace McpAuthLab.Tests;

/// <summary>
/// The protected MCP server (port 5900) and the test authorization server (port 5901), both on in-memory
/// <c>TestServer</c>s. A routing handler picks the server by port, and a logging handler writes the wire log.
/// </summary>
public sealed class LabEnvironment : IAsyncDisposable
{
    private readonly WebApplicationFactory<AuthServerMarker> _as;
    private readonly WebApplicationFactory<ServerMarker> _mcp;

    public LabEnvironment(Dictionary<string, string?>? authServerSettings = null)
    {
        _as = new WebApplicationFactory<AuthServerMarker>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services => services.AddLogging(l => l.ClearProviders()));
            foreach (var (key, value) in authServerSettings ?? [])
            {
                b.UseSetting(key, value);
            }
        });
        var asHandler = _as.Server.CreateHandler();

        // The server fetches the signing keys of the authorization server: give it the in-memory route.
        // Configure (not PostConfigure) runs before the JWT bearer post-configuration builds its backchannel.
        _mcp = new WebApplicationFactory<ServerMarker>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.AddLogging(l => l.ClearProviders());
                services.AddDataProtection().UseEphemeralDataProtectionProvider();   // no keys written to the user profile
                services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                    .Configure(o => o.BackchannelHttpHandler = asHandler);
            });
        });
        Routes = new Dictionary<int, HttpMessageHandler> { [5900] = _mcp.Server.CreateHandler(), [5901] = asHandler };
    }

    public WireLog Wire { get; } = new();

    public IReadOnlyDictionary<int, HttpMessageHandler> Routes { get; }

    public AuthDecisions Decisions => _mcp.Services.GetRequiredService<AuthDecisions>();

    /// <summary>An HttpClient that reaches both servers and writes what it sends and receives to <see cref="Wire"/>.</summary>
    public HttpClient NewHttpClient(bool logged = true)
    {
        HttpMessageHandler routing = new RoutingHandler(Routes);
        return new HttpClient(logged ? new WireLogHandler(Wire) { InnerHandler = routing } : routing);
    }

    /// <summary>The "browser": follows the authorization URI without following the redirect, returns the Location.</summary>
    private static async Task<AuthorizationResult?> BrowseAsync(HttpClient http, Uri authorizationUri, CancellationToken ct)
    {
        var response = await http.GetAsync(authorizationUri, ct);
        var q = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        return new AuthorizationResult { Code = q["code"], State = q["state"], Iss = q["iss"] };
    }

    /// <summary>An MCP client that runs the whole OAuth flow when the server answers 401.</summary>
    public async Task<McpClient> ConnectAsync(CancellationToken ct)
    {
        var http = NewHttpClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(LabUris.McpEndpoint),
            OAuth = new ClientOAuthOptions
            {
                ClientId = LabUris.ClientId,
                ClientSecret = LabUris.ClientSecret,
                RedirectUri = new Uri(LabUris.ClientCallback),
                AuthorizationCallbackHandler = (context, token) => BrowseAsync(http, context.AuthorizationUri, token),
            },
        }, http, ownsHttpClient: false);
        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }

    /// <summary>The flow by hand, so that a test can ask for any resource and scope, or break one step.</summary>
    public async Task<HttpResponseMessage> RequestTokenAsync(
        string? resource, string scope = LabUris.Scope, string? verifierOnToken = null, string? resourceOnToken = "same")
    {
        using var http = NewHttpClient(logged: false);
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var query = $"client_id={LabUris.ClientId}&redirect_uri={Uri.EscapeDataString(LabUris.ClientCallback)}&response_type=code"
                    + $"&code_challenge={challenge}&code_challenge_method=S256&scope={Uri.EscapeDataString(scope)}&state=s1"
                    + (resource is null ? "" : $"&resource={Uri.EscapeDataString(resource)}");
        var authorize = await http.GetAsync($"{LabUris.AuthorizationServer}/authorize?{query}");
        var location = authorize.Headers.Location!;
        var code = HttpUtility.ParseQueryString(location.Query)["code"];
        if (code is null)
        {
            return authorize;   // the server refused to issue a code: the redirect carries the error
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = LabUris.ClientCallback,
            ["code_verifier"] = verifierOnToken ?? verifier,
            ["client_id"] = LabUris.ClientId,
            ["client_secret"] = LabUris.ClientSecret,
        };
        var tokenResource = resourceOnToken == "same" ? resource : resourceOnToken;
        if (tokenResource is not null)
        {
            form["resource"] = tokenResource;
        }

        return await http.PostAsync($"{LabUris.AuthorizationServer}/token", new FormUrlEncodedContent(form));
    }

    /// <summary>POSTs an MCP request with a bearer token (or none) straight to the protected server.</summary>
    public async Task<HttpResponseMessage> PostToolsListAsync(string? bearer)
    {
        using var http = NewHttpClient(logged: false);
        var request = new HttpRequestMessage(HttpMethod.Post, LabUris.McpEndpoint)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (bearer is not null)
        {
            request.Headers.Authorization = new("Bearer", bearer);
        }

        return await http.SendAsync(request);
    }

    public async Task<string> AccessTokenAsync(string resource, string scope = LabUris.Scope)
    {
        var response = await RequestTokenAsync(resource, scope);
        return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("access_token").GetString()!;
    }

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async ValueTask DisposeAsync()
    {
        await _mcp.DisposeAsync();
        await _as.DisposeAsync();
    }
}
