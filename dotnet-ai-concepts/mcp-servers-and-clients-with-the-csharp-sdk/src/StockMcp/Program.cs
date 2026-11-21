using ModelContextProtocol.AspNetCore;
using StockMcp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<StockStore>();

// TODO-06: Add the MCP server with the HTTP transport, using the session mode from the configuration key
//          "Mcp:SessionMode" (StockServer.ParseSessionMode), and the stock capabilities. Then map the
//          endpoint at /mcp below.

var app = builder.Build();
app.Run();

public partial class Program;
