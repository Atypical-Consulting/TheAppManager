using Microsoft.Extensions.Hosting;
using TheAppManager.Sample.Console.Modules;

namespace TheAppManager.Sample.Console;

/// <summary>The console composition: IAppModule-only modules, no web types.</summary>
public static class ConsoleModules
{
    public static void Configure(AppModuleCollection modules) => modules
        .Add<ForecastModule>()
        .AddIf<VerboseLoggingModule>(b => b.Environment.IsDevelopment());
}
