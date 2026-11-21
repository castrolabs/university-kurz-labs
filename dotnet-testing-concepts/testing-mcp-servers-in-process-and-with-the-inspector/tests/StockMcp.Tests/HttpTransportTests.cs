namespace StockMcp.Tests;

// TODO-03: The same tools over HTTP, on the in-memory TestServer. This catches what the pipes cannot: the MapMcp
// route and the session mode. Keep one or two tests here and the bulk on the pipes.
[TestClass]
[TestCategory("Integration")]
public sealed class HttpTransportTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task ShouldServeTheToolsOverHttpWithNoSessionInStatelessMode()
    {
        // WebApplicationFactory<Program> gives the HttpClient; HttpClientTransport accepts it (ownsHttpClient: false),
        // and the URL only needs the right path: http://localhost/mcp. Call get_stock, then assert the text, the
        // negotiated protocol version (McpClient.NegotiatedProtocolVersion) and that there is no session id.
        await Task.CompletedTask;
        Assert.Fail("TODO-03: write this test.");
    }

    [TestMethod]
    public async Task ShouldRefuseAPlainGetOnTheMcpRoute()
    {
        // A plain GET /mcp on a stateless server is not an MCP stream. Assert the exact status code.
        await Task.CompletedTask;
        Assert.Fail("TODO-03: write this test.");
    }
}
