using AuthServerLab;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthServer();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthServer();
app.Run();

public partial class Program;
