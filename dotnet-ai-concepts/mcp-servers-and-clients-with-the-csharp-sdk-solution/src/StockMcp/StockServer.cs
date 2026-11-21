using ModelContextProtocol.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StockMcp;

public static class StockServer
{
    /// <summary>Everything the stock server offers: tools, prompts and resources.</summary>
    public static IMcpServerBuilder AddStockCapabilities(this IMcpServerBuilder builder) =>
        builder.WithTools<StockTools>().WithPrompts<StockPrompts>().WithResources<StockResources>();

    /// <summary>Reads "Stateless", "Stateful" or "StatefulForInitializeClients"; missing means Stateless.</summary>
    public static HttpServerSessionMode ParseSessionMode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? HttpServerSessionMode.Stateless
            : Enum.TryParse<HttpServerSessionMode>(value, ignoreCase: true, out var mode)
                ? mode
                : throw new ArgumentException($"Unknown session mode '{value}'.", nameof(value));

    /// <summary>stdout is the protocol channel on stdio: every log line goes to stderr.</summary>
    public static ILoggingBuilder LogToStandardError(this ILoggingBuilder logging) =>
        logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    public static async Task RunStdioAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.LogToStandardError();
        builder.Services.AddSingleton<StockStore>();
        builder.Services.AddMcpServer().WithStdioServerTransport().AddStockCapabilities();
        await builder.Build().RunAsync();
    }
}
