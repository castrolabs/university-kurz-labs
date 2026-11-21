namespace StockMcp.Tests;

// TODO-04: Time. McpClientOptions.TimeProvider drives InitializationTimeout (60 seconds by default) and
// DiscoverProbeTimeout (5 seconds): a FakeTimeProvider tests them in milliseconds. It does not reach
// HttpClient.Timeout, OAuth token expiry or HttpClientTransportOptions.ConnectionTimeout, which need their own tests.
// ScriptedPeer (given) is the other end of the pipes: the test plays the server, line by line. Read it first.
[TestClass]
[TestCategory("Time")]
public sealed class ClientTimeoutTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task ShouldTimeOutTheInitializationOfAServerThatSaysNothing()
    {
        // Pin ProtocolVersion = "2025-11-25" (no discover probe) and a FakeTimeProvider; start McpClient.CreateAsync
        // WITHOUT awaiting it. Read the first request from the peer before advancing the clock: once the client has
        // written initialize, its timer exists, so Advance cannot run too early. Advance(59 s): still connecting.
        // Advance(2 s) more: TimeoutException. Never Thread.Sleep and never Task.Delay.
        await Task.CompletedTask;
        Assert.Fail("TODO-04: write this test.");
    }

    [TestMethod]
    public async Task ShouldFallBackToInitializeWhenAnOldServerIgnoresTheDiscoverProbe()
    {
        // Default options (they probe with server/discover) plus the fake clock. The peer reads server/discover and
        // stays silent. Advance(5.1 s). Then read what the client sends next (notifications/cancelled, then
        // initialize: collect the methods until you see initialize) and answer it with
        // peer.WriteInitializeResultAsync. Assert the order of the methods, the negotiated version (2025-11-25)
        // and that the fake clock moved by 5.1 seconds. An old-server test in milliseconds, not in 5 seconds.
        await Task.CompletedTask;
        Assert.Fail("TODO-04: write this test.");
    }
}
