using ModelContextProtocol.Client;

namespace StockMcp.Tests;

/// <summary>
/// The real server and the real <see cref="McpClient"/>, joined by two in-memory pipes. No process, no port.
/// </summary>
public sealed class InProcessMcp : IAsyncDisposable
{
    public McpClient Client => throw new NotImplementedException("TODO-00: build InProcessMcp.");

    // TODO-00: Start the stock server and connect the real client to it without a process or a port.
    //   - Two System.IO.Pipelines.Pipe objects: the client's writes are the server's reads, and the other way round.
    //     StreamServerTransport / StreamClientTransport (WithStreamServerTransport on the builder) take Streams;
    //     Reader.AsStream() and Writer.AsStream() give them.
    //   - The server sits on a generic host (Host.CreateApplicationBuilder) so that it starts as a hosted service,
    //     as it does over stdio. Register StockStore as a singleton and the tools class given as TTools (WithTools<T>).
    //   - Start the host, then McpClient.CreateAsync with the client end of the pipes and the options given.
    //   - DisposeAsync disposes the client, stops the host and disposes it.
    // Every test builds its own pair: a few milliseconds, and nothing is shared between tests.
    public static Task<InProcessMcp> StartAsync<TTools>(McpClientOptions? options = null, CancellationToken ct = default)
        where TTools : class =>
        throw new NotImplementedException("TODO-00: build InProcessMcp.");

    public static Task<InProcessMcp> StartAsync(McpClientOptions? options = null, CancellationToken ct = default) =>
        StartAsync<StockTools>(options, ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
