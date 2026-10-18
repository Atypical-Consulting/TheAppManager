using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TheAppManager.Sample.Console.Modules;

/// <summary>Raises the log level to Debug (registered with AddIf in Development only).</summary>
public sealed class VerboseLoggingModule : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder) => builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
