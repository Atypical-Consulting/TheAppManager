using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager.Sample.Console.Modules;

/// <summary>Binds validated forecast options and registers the table renderer.</summary>
public sealed class ForecastModule : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<ForecastOptions>()
            .BindConfiguration("Forecast")
            .Validate(o => o.Days is >= 1 and <= 14, "Forecast:Days must be between 1 and 14.")
            .ValidateOnStart();
        builder.Services.AddSingleton<ForecastTable>();
    }
}
