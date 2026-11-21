using ModelContextProtocol.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StockMcp;

public static class StockServer
{
    /// <summary>Everything the stock server offers: tools, prompts and resources.</summary>
    public static IMcpServerBuilder AddStockCapabilities(this IMcpServerBuilder builder)
    {
        // TODO-04: Register StockTools, StockPrompts and StockResources on the builder (the explicit
        //          WithTools / WithPrompts / WithResources calls, not an assembly scan) and return it.
        return builder;
    }

    /// <summary>Reads "Stateless", "Stateful" or "StatefulForInitializeClients"; missing means Stateless.</summary>
    public static HttpServerSessionMode ParseSessionMode(string? value)
    {
        // TODO-05: Null or blank gives HttpServerSessionMode.Stateless. Otherwise parse the name ignoring
        //          case, and throw ArgumentException for a name that is not a mode.
        throw new NotImplementedException();
    }

    public static async Task RunStdioAsync(string[] args)
    {
        // TODO-09 (optional): Run the stock server over stdio on a generic host: log everything to standard
        //          error (stdout carries JSON-RPC, a stray Console.WriteLine corrupts it), register the
        //          StockStore, WithStdioServerTransport and AddStockCapabilities, then RunAsync.
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
