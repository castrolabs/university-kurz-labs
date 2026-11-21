using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace StockMcp.Tests;

public sealed class StockMcpTests
{
    private static CancellationToken Ct => new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

    private static Dictionary<string, object?> Args(string sku) => new() { ["sku"] = sku };

    // --- the public contract: tools/list ---

    [Fact]
    public async Task ShouldListTheThreeToolsWithTheirDescriptions()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(["get_note", "get_stock", "reserve"], tools.Select(t => t.Name).Order());
        Assert.Equal("Returns units in stock for a SKU.", tools.Single(t => t.Name == "get_stock").Description);
        Assert.Equal("Reserves units of a SKU.", tools.Single(t => t.Name == "reserve").Description);
    }

    [Fact]
    public async Task ShouldBuildTheSchemaFromTheParametersAndTheirDescriptions()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);
        var schema = tools.Single(t => t.Name == "get_stock").JsonSchema;

        var sku = schema.GetProperty("properties").GetProperty("sku");
        Assert.Equal("The SKU, for example SKU-1", sku.GetProperty("description").GetString());
        Assert.Equal("string", sku.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ShouldKeepServicesInjectedIntoTheMethodOutOfTheSchema()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);
        var properties = tools.Single(t => t.Name == "get_note").JsonSchema.GetProperty("properties");

        // ILogger<StockTools> comes from DI, so only the order id is an argument.
        Assert.Equal(["orderId"], properties.EnumerateObject().Select(p => p.Name));
        Assert.Equal("integer", properties.GetProperty("orderId").GetProperty("type").GetString());
    }

    // --- calling tools: results and errors ---

    [Fact]
    public async Task ShouldReturnTheTextAndLeaveIsErrorNullOnSuccess()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync("get_stock", Args("SKU-1"), cancellationToken: Ct);

        Assert.Equal("SKU-1: 12 units", StockClient.TextOf(result));
        Assert.Null(result.IsError);   // null on success, not false
    }

    [Fact]
    public async Task ShouldHideTheMessageOfAnOrdinaryException()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync("get_stock", Args("NOPE"), cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Equal("An error occurred invoking 'get_stock'.", StockClient.TextOf(result));
    }

    [Fact]
    public async Task ShouldShowTheMessageOfAnMcpException()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync(
            "reserve", new Dictionary<string, object?> { ["sku"] = "SKU-1", ["quantity"] = 0 }, cancellationToken: Ct);

        Assert.True(result.IsError);
        Assert.Equal("An error occurred invoking 'reserve': quantity must be positive", StockClient.TextOf(result));
        Assert.Empty(mcp.Store.Reservations);
    }

    [Fact]
    public async Task ShouldThrowInvalidParamsForAToolThatDoesNotExist()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var ex = await Assert.ThrowsAsync<McpProtocolException>(() =>
            mcp.Client.CallToolAsync("missing_tool", cancellationToken: Ct).AsTask());

        Assert.Equal(McpErrorCode.InvalidParams, ex.ErrorCode);
        Assert.Contains("Unknown tool: 'missing_tool'", ex.Message);
    }

    // --- prompts and resources ---

    [Fact]
    public async Task ShouldServeThePromptAndTheResource()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var prompt = await mcp.Client.GetPromptAsync("restock_plan", Args("SKU-2")!, cancellationToken: Ct);
        var resource = await mcp.Client.ReadResourceAsync("stock://policy", cancellationToken: Ct);

        Assert.Equal("Write a three-step restock plan for SKU-2.", ((TextContentBlock)prompt.Messages.Single().Content).Text);
        Assert.Equal("Reservations expire after 24 hours.", ((TextResourceContents)resource.Contents.Single()).Text);
    }

    // --- tools are AIFunctions, and tool output is data ---

    [Fact]
    public async Task ShouldOfferOnlyTheAllowedToolsAndWrapTheOnesThatNeedApproval()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);
        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);

        var forModel = StockClient.ToolsForModel(
            tools, allowed: new HashSet<string> { "get_stock", "reserve" }, requireApproval: new HashSet<string> { "reserve" });

        Assert.Equal(["get_stock", "reserve"], forModel.Select(t => t.Name).Order());
        Assert.IsType<McpClientTool>(forModel.Single(t => t.Name == "get_stock"));
        Assert.IsType<ApprovalRequiredAIFunction>(forModel.Single(t => t.Name == "reserve"));
    }

    [Fact]
    public async Task ShouldReturnAnInjectedNoteVerbatimAndUnflagged()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync(
            "get_note", new Dictionary<string, object?> { ["orderId"] = 7 }, cancellationToken: Ct);

        Assert.Null(result.IsError);
        Assert.Contains("IGNORE ALL PREVIOUS INSTRUCTIONS", StockClient.TextOf(result));
    }

    [Fact]
    public async Task ShouldStopTheInjectedReserveOnlyWhenItNeedsApproval()
    {
        await using var plain = await InProcessMcp.StartAsync(ct: Ct);
        await using var guarded = await InProcessMcp.StartAsync(ct: Ct);
        using IChatClient chat = new ChatClientBuilder(new ScriptedChatClient()).UseFunctionInvocation().Build();
        var allTools = await plain.Client.ListToolsAsync(cancellationToken: Ct);
        var guardedTools = StockClient.ToolsForModel(
            await guarded.Client.ListToolsAsync(cancellationToken: Ct),
            new HashSet<string> { "get_stock", "reserve" }, new HashSet<string> { "reserve" });

        await chat.GetResponseAsync("Read the customer note for order 7", new ChatOptions { Tools = [.. allTools] }, Ct);
        var response = await chat.GetResponseAsync("Read the customer note for order 7", new ChatOptions { Tools = guardedTools }, Ct);

        Assert.Equal(["999 x SKU-1"], plain.Store.Reservations);   // the plain tool ran without asking anyone
        Assert.Single(response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>());
        Assert.Empty(guarded.Store.Reservations);                  // the wrapped one never reached the server
    }

    // --- protocol versions and Streamable HTTP ---

    [Fact]
    public async Task ShouldNegotiateTheNewestRevisionByDefaultAndAnOldOneWhenPinned()
    {
        await using var current = await InProcessMcp.StartAsync(ct: Ct);
        await using var pinned = await InProcessMcp.StartAsync(new McpClientOptions { ProtocolVersion = "2025-11-25" }, Ct);

        Assert.Equal("2026-07-28", current.Client.NegotiatedProtocolVersion);
        Assert.Equal("2025-11-25", pinned.Client.NegotiatedProtocolVersion);
    }

    [Fact]
    public async Task ShouldServeStatelessHttpWithNoSessionAndRefuseAGet()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var http = factory.CreateClient();
        await using var client = await ConnectAsync(http);

        var result = await client.CallToolAsync("get_stock", Args("SKU-1"), cancellationToken: Ct);

        Assert.Equal("SKU-1: 12 units", StockClient.TextOf(result));
        Assert.Null(client.SessionId);
        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, (await http.GetAsync("/mcp", Ct)).StatusCode);
    }

    [Fact]
    public async Task ShouldMintASessionInStatefulModeForAPinnedClientAndDowngradeADefaultOne()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            b => b.UseSetting("Mcp:SessionMode", "Stateful"));
        using var http = factory.CreateClient();

        await using var pinned = await ConnectAsync(http, new McpClientOptions { ProtocolVersion = "2025-11-25" });
        await using var current = await ConnectAsync(http);

        Assert.False(string.IsNullOrEmpty(pinned.SessionId));
        Assert.Equal("2025-11-25", current.NegotiatedProtocolVersion);   // server/discover got -32022, the client fell back
    }

    private static async Task<McpClient> ConnectAsync(HttpClient http, McpClientOptions? options = null) =>
        await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri("http://localhost/mcp") }, http, ownsHttpClient: false),
            options ?? new McpClientOptions(),
            cancellationToken: Ct);
}
