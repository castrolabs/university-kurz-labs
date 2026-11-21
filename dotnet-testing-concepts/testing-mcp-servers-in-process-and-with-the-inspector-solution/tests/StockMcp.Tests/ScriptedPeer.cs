using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace StockMcp.Tests;

/// <summary>
/// The other end of the pipes, played by the test: it reads the client's JSON-RPC lines and writes whatever the test
/// wants back, or nothing at all. It reads and writes the raw protocol, so it breaks if the SDK changes its first
/// message. Use it only for a peer that misbehaves or is old; the real server is better for everything else.
/// </summary>
public sealed class ScriptedPeer
{
    private readonly Pipe _clientToPeer = new();
    private readonly Pipe _peerToClient = new();
    private readonly StreamReader _reader;

    public ScriptedPeer()
    {
        _reader = new StreamReader(_clientToPeer.Reader.AsStream(), Encoding.UTF8);
        Transport = new StreamClientTransport(_clientToPeer.Writer.AsStream(), _peerToClient.Reader.AsStream());
    }

    public IClientTransport Transport { get; }

    /// <summary>The next message the client wrote, as JSON. Waits for it: that wait is the synchronization with the client.</summary>
    public async Task<JsonNode> ReadRequestAsync(CancellationToken ct)
    {
        var line = await _reader.ReadLineAsync(ct) ?? throw new InvalidOperationException("The client closed the connection.");
        return JsonNode.Parse(line)!;
    }

    public async Task WriteAsync(string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _peerToClient.Writer.WriteAsync(bytes, ct);
        await _peerToClient.Writer.FlushAsync(ct);
    }

    /// <summary>The answer of a 2025-11-25 server to `initialize`.</summary>
    public Task WriteInitializeResultAsync(JsonNode request, CancellationToken ct) =>
        WriteAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = request["id"]!.DeepClone(),
            ["result"] = new JsonObject
            {
                ["protocolVersion"] = "2025-11-25",
                ["capabilities"] = new JsonObject(),
                ["serverInfo"] = new JsonObject { ["name"] = "OldServer", ["version"] = "1.0.0" },
            },
        }.ToJsonString(), ct);
}
