using TheAppManager;
using TheAppManager.Testing.WebApp;

// NO_ARGS=1 simulates an app that ignores args, which WebApplicationFactory settings cannot reach.
var builder = Environment.GetEnvironmentVariable("NO_ARGS") == "1"
    ? WebApplication.CreateBuilder()
    : WebApplication.CreateBuilder(args);

builder.AddModules(m => m.Add<PingModule>().Add<ExtraModule>());

var app = builder.Build();
app.UseModules().MapModules();
app.Run();

/// <summary>The entry point, public so tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
