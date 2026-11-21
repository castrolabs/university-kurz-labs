using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StockMcp.Tests;

/// <summary>
/// The real stock server and the real <see cref="McpClient"/>, joined by two in-memory pipes.
/// No process, no port: the client's writes are the server's reads and the other way round.
/// </summary>
public sealed class InProcessMcp : IAsyncDisposable
{
    private readonly IHost _host;

    private InProcessMcp(IHost host, McpClient client, StockStore store)
    {
        _host = host;
        Client = client;
        Store = store;
    }

    public McpClient Client { get; }

    public StockStore Store { get; }

    public static async Task<InProcessMcp> StartAsync(McpClientOptions? options = null, CancellationToken ct = default)
    {
        Pipe clientToServer = new(), serverToClient = new();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<StockStore>();
        builder.Services.AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .AddStockCapabilities();
        var host = builder.Build();
        await host.StartAsync(ct);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            options ?? new McpClientOptions(),
            cancellationToken: ct);

        return new InProcessMcp(host, client, host.Services.GetRequiredService<StockStore>());
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }
}
