using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace StockMcp.Tests;

// The same tools over HTTP, on the in-memory TestServer: what the pipes cannot see (routing, the session mode).
[TestClass]
[TestCategory("Integration")]
public sealed class HttpTransportTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    private async Task<McpClient> ConnectAsync(HttpClient http) =>
        await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri("http://localhost/mcp") }, http, ownsHttpClient: false),
            cancellationToken: Ct);

    [TestMethod]
    public async Task ShouldServeTheToolsOverHttpWithNoSessionInStatelessMode()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var http = factory.CreateClient();

        await using var client = await ConnectAsync(http);
        var result = await client.CallToolAsync("get_stock", new Dictionary<string, object?> { ["sku"] = "SKU-1" }, cancellationToken: Ct);

        Assert.AreEqual("2026-07-28", client.NegotiatedProtocolVersion);
        Assert.IsNull(client.SessionId);
        Assert.AreEqual("SKU-1: 12 units", ((TextContentBlock)result.Content.Single()).Text);
    }

    [TestMethod]
    public async Task ShouldRefuseAPlainGetOnTheMcpRoute()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/mcp", Ct);

        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
