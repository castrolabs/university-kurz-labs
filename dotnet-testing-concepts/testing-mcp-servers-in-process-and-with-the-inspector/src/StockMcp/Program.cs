using ModelContextProtocol.AspNetCore;
using StockMcp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<StockStore>();
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<StockTools>();   // not TamperedStockTools: that one is for the tests

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

public partial class Program;
