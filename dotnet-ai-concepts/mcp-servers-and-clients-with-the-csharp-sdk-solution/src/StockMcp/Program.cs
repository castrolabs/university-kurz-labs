using ModelContextProtocol.AspNetCore;
using StockMcp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<StockStore>();
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = StockServer.ParseSessionMode(builder.Configuration["Mcp:SessionMode"]))
    .AddStockCapabilities();

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

public partial class Program;
