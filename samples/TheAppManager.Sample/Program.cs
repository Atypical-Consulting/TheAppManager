using TheAppManager;
using TheAppManager.Sample;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddModules(AppModules.Configure);

var app = builder.Build();
app.UseModules();
app.MapDefaultEndpoints();
app.MapGroup("/api").RequireAuthorization().MapModules();
app.Run();
