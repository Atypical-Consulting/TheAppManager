using TheAppManager.Sample.Forecast;

namespace TheAppManager.Sample.Modules;

/// <summary>Registers weather forecast options, services and endpoints.</summary>
public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<WeatherOptions>()
            .BindConfiguration("Weather")
            .Validate(o => o.Days is >= 1 and <= 14, "Weather:Days must be between 1 and 14.")
            .ValidateOnStart();
        builder.Services.AddScoped<WeatherForecastService>();
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGetWeatherForecasts();
}
