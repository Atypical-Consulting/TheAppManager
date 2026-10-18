using TheAppManager;

var builder = WebApplication.CreateSlimBuilder(args);
builder.AddModules(m => m.Add<PingModule>());
var app = builder.Build();
app.UseModules();
app.MapModules();
app.Run();

internal sealed class PingModule : IWebModule
{
    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/ping", () => "pong");
}
