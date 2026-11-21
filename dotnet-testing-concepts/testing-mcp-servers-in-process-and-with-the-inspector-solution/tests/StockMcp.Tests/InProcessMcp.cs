using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StockMcp.Tests;

/// <summary>
/// The real server and the real <see cref="McpClient"/>, joined by two in-memory pipes: the client's writes are the
/// server's reads and the other way round. No process, no port. The server sits on a generic host, which starts it
/// as a hosted service, as in production over stdio.
/// </summary>
public sealed class InProcessMcp : IAsyncDisposable
{
    private readonly IHost _host;

    private InProcessMcp(IHost host, McpClient client)
    {
        _host = host;
        Client = client;
    }

    public McpClient Client { get; }

    public static async Task<InProcessMcp> StartAsync<TTools>(McpClientOptions? options = null, CancellationToken ct = default)
        where TTools : class
    {
        Pipe clientToServer = new(), serverToClient = new();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<StockStore>();
        builder.Services.AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithTools<TTools>();
        var host = builder.Build();
        await host.StartAsync(ct);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            options ?? new McpClientOptions(),
            cancellationToken: ct);

        return new InProcessMcp(host, client);
    }

    public static Task<InProcessMcp> StartAsync(McpClientOptions? options = null, CancellationToken ct = default) =>
        StartAsync<StockTools>(options, ct);

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }
}
