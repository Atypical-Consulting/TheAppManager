using Microsoft.Extensions.Options;

namespace TheAppManager.Sample.Forecast;

public class WeatherForecastService(IOptions<WeatherOptions> options)
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public WeatherForecast[] GetForecasts()
    {
        return Enumerable
            .Range(1, options.Value.Days)
            .Select(index => new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]))
            .ToArray();
    }
}
