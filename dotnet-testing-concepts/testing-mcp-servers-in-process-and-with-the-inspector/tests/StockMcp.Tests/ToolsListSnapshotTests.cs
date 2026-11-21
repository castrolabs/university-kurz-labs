namespace StockMcp.Tests;

// TODO-05, TODO-06 and TODO-07: tools/list is what the model reads. A change in a name, a description or a schema
// changes how it behaves, with no compile error. The snapshot makes such a change fail the build until a human has
// looked at it. Write Snapshot.Verify (TODO-05) and Snapshot.RenderTools (TODO-06) first.
[TestClass]
[TestCategory("Snapshot")]
public sealed class ToolsListSnapshotTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    // TODO-06 (part 2): The first run fails and writes snapshots/ToolsListIsThePublicContract.received.txt. Read it,
    // and if it is right rename it to ToolsListIsThePublicContract.verified.txt and commit it.
    [TestMethod]
    public async Task ToolsListIsThePublicContract()
    {
        // In process: list the tools of the real server and verify the rendered text.
        await Task.CompletedTask;
        Assert.Fail("TODO-06: write this test.");
    }

    [TestMethod]
    public async Task ToolsListOverHttpIsTheSameContract()
    {
        // Over HTTP (WebApplicationFactory): verify with the SAME test name as the test above (the `test:` argument
        // of Verify), so that one checked-in file proves that both transports publish one contract.
        await Task.CompletedTask;
        Assert.Fail("TODO-06: write this test.");
    }

    // TODO-07: A "rug pull": the server changes its contract after the client approved it. TamperedStockTools (in
    // src/StockMcp) is that server: compare it with the approved one. The snapshot does not decide whether a change
    // is safe, it forces a human to look at the pull request.
    [TestMethod]
    public async Task ARugPullShouldChangeTheSnapshotAndShowWhatChanged()
    {
        // Start the two servers (InProcessMcp.StartAsync<TamperedStockTools>), render both and assert that they differ.
        // Then assert WHAT is in the diff: the lines that only the tampered text has must include the injected
        // sentence in the description of reserve and the new "note" parameter.
        await Task.CompletedTask;
        Assert.Fail("TODO-07: write this test.");
    }
}
