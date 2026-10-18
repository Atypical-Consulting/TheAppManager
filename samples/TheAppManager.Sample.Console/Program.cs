using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheAppManager;
using TheAppManager.Sample.Console;

var builder = Host.CreateApplicationBuilder(args);
builder.AddModules(ConsoleModules.Configure);

using var host = builder.Build();
await host.StartAsync(); // runs ValidateOnStart and logs the module plan
host.Services.GetRequiredService<ForecastTable>().Render();
await host.StopAsync();
