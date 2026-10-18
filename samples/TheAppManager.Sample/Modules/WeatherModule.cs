using TheAppManager;
using TheAppManager.Sample.Forecast;

namespace TheAppManager.Sample.Modules;

/// <summary>
/// Registers weather forecast services and endpoints.
/// </summary>
public class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<WeatherForecastService>();
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetWeatherForecasts();
    }
}
