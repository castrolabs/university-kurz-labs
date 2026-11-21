namespace StockMcp.Tests;

// TODO-01 and TODO-02: the contract of the server over the in-memory pipes (InProcessMcp, TODO-00).
// Every test builds its own InProcessMcp and passes testContext.CancellationToken to every call.
[TestClass]
[TestCategory("Contract")]
public sealed class ToolsListContractTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    // TODO-01: What tools/list says. A host shows the model the name, the description and the schema.
    [TestMethod]
    public async Task ShouldListTheTwoToolsWithTheirDescriptions()
    {
        // ListToolsAsync: the names are get_stock and reserve, each with the description written in StockTools.
        await Task.CompletedTask;
        Assert.Fail("TODO-01: write this test.");
    }

    [TestMethod]
    public async Task ShouldDescribeEachParameterInTheInputSchema()
    {
        // McpClientTool.JsonSchema is a JsonElement: find the "sku" property of get_stock, its type and its description.
        await Task.CompletedTask;
        Assert.Fail("TODO-01: write this test.");
    }

    [TestMethod]
    public async Task ShouldLeaveIsErrorNullNotFalseOnSuccess()
    {
        // Call get_stock for SKU-1 and read the text blocks of the result. IsError is a bool?: what is it on
        // success? Choose an assertion that says so (Assert.IsFalse would not even compile, and
        // Assert.AreEqual(false, ...) would fail).
        await Task.CompletedTask;
        Assert.Fail("TODO-01: write this test.");
    }

    // TODO-02: An ordinary exception, an McpException and an unknown tool look alike from far away. They are three contracts.
    [TestMethod]
    public async Task ShouldHideTheMessageOfAnOrdinaryExceptionFromTheClient()
    {
        // get_stock for an unknown SKU: the tool throws KeyNotFoundException. The result has IsError = true and a
        // generic text. Assert the exact text: the message of the exception must not be in it.
        await Task.CompletedTask;
        Assert.Fail("TODO-02: write this test.");
    }

    [TestMethod]
    public async Task ShouldShowTheMessageOfAnMcpExceptionOnPurpose()
    {
        // reserve with quantity 0: IsError = true, and this time the text includes the message of the McpException.
        await Task.CompletedTask;
        Assert.Fail("TODO-02: write this test.");
    }

    [TestMethod]
    public async Task ShouldThrowForAToolThatDoesNotExistInsteadOfReturningAnError()
    {
        // An unknown tool is not a tool result but a JSON-RPC error: CallToolAsync throws. Assert the exact
        // exception type (Assert.ThrowsExactlyAsync) and its ErrorCode (McpErrorCode.InvalidParams).
        await Task.CompletedTask;
        Assert.Fail("TODO-02: write this test.");
    }
}
