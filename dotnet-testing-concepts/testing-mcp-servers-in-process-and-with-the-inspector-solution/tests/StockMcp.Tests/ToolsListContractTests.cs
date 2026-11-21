using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace StockMcp.Tests;

// The contract of the server over the in-memory pipes: what tools/list says, and how failures look from the client.
[TestClass]
[TestCategory("Contract")]
public sealed class ToolsListContractTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    private static string Text(CallToolResult result) =>
        string.Join(" ", result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    private static Dictionary<string, object?> Args(string sku) => new() { ["sku"] = sku };

    [TestMethod]
    public async Task ShouldListTheTwoToolsWithTheirDescriptions()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);

        CollectionAssert.AreEqual(new[] { "get_stock", "reserve" }, tools.Select(t => t.Name).Order().ToArray());
        Assert.AreEqual("Returns units in stock for a SKU.", tools.Single(t => t.Name == "get_stock").Description);
        Assert.AreEqual("Reserves units of a SKU.", tools.Single(t => t.Name == "reserve").Description);
    }

    [TestMethod]
    public async Task ShouldDescribeEachParameterInTheInputSchema()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: Ct);
        var properties = tools.Single(t => t.Name == "get_stock").JsonSchema.GetProperty("properties");

        Assert.AreEqual("The SKU, for example SKU-1", properties.GetProperty("sku").GetProperty("description").GetString());
        Assert.AreEqual("string", properties.GetProperty("sku").GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task ShouldLeaveIsErrorNullNotFalseOnSuccess()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync("get_stock", Args("SKU-1"), cancellationToken: Ct);

        Assert.AreEqual("SKU-1: 12 units", Text(result));
        Assert.AreNotEqual(true, result.IsError);   // null on success, so IsFalse(result.IsError) would not even compile
    }

    [TestMethod]
    public async Task ShouldHideTheMessageOfAnOrdinaryExceptionFromTheClient()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync("get_stock", Args("NOPE"), cancellationToken: Ct);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("An error occurred invoking 'get_stock'.", Text(result));   // the KeyNotFoundException text stays on the server
    }

    [TestMethod]
    public async Task ShouldShowTheMessageOfAnMcpExceptionOnPurpose()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var result = await mcp.Client.CallToolAsync(
            "reserve", new Dictionary<string, object?> { ["sku"] = "SKU-1", ["quantity"] = 0 }, cancellationToken: Ct);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("An error occurred invoking 'reserve': quantity must be positive", Text(result));
    }

    [TestMethod]
    public async Task ShouldThrowForAToolThatDoesNotExistInsteadOfReturningAnError()
    {
        await using var mcp = await InProcessMcp.StartAsync(ct: Ct);

        var ex = await Assert.ThrowsExactlyAsync<McpProtocolException>(() =>
            mcp.Client.CallToolAsync("missing_tool", cancellationToken: Ct).AsTask());

        Assert.AreEqual(McpErrorCode.InvalidParams, ex.ErrorCode);
        StringAssert.Contains(ex.Message, "Unknown tool: 'missing_tool'");
    }
}
