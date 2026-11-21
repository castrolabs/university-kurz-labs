using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol.Client;

namespace StockMcp.Tests;

// McpClientOptions.TimeProvider drives InitializationTimeout (60 s) and DiscoverProbeTimeout (5 s). It does not
// reach HttpClient.Timeout, OAuth token expiry or HttpClientTransportOptions.ConnectionTimeout.
[TestClass]
[TestCategory("Time")]
public sealed class ClientTimeoutTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task ShouldTimeOutTheInitializationOfAServerThatSaysNothing()
    {
        var peer = new ScriptedPeer();
        var clock = new FakeTimeProvider();
        var options = new McpClientOptions { TimeProvider = clock, ProtocolVersion = "2025-11-25" };   // pinned: no discover probe

        var connect = McpClient.CreateAsync(peer.Transport, options, cancellationToken: Ct);
        Assert.AreEqual("initialize", (await peer.ReadRequestAsync(Ct))["method"]!.ToString());   // the timer exists once this arrives

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.IsFalse(connect.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => connect);
    }

    [TestMethod]
    public async Task ShouldFallBackToInitializeWhenAnOldServerIgnoresTheDiscoverProbe()
    {
        var peer = new ScriptedPeer();
        var clock = new FakeTimeProvider();
        var started = clock.GetUtcNow();

        var connect = McpClient.CreateAsync(peer.Transport, new McpClientOptions { TimeProvider = clock }, cancellationToken: Ct);
        Assert.AreEqual("server/discover", (await peer.ReadRequestAsync(Ct))["method"]!.ToString());
        clock.Advance(TimeSpan.FromSeconds(5.1));

        var seen = new List<string>();
        System.Text.Json.Nodes.JsonNode request;
        do
        {
            request = await peer.ReadRequestAsync(Ct);
            seen.Add(request["method"]!.ToString());
        }
        while (seen[^1] != "initialize");
        await peer.WriteInitializeResultAsync(request, Ct);

        await using var client = await connect;
        CollectionAssert.AreEqual(new[] { "notifications/cancelled", "initialize" }, seen);
        Assert.AreEqual("2025-11-25", client.NegotiatedProtocolVersion);
        Assert.AreEqual(TimeSpan.FromSeconds(5.1), clock.GetUtcNow() - started);   // fake seconds: nothing was waited for
    }
}
