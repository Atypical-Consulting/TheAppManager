using Microsoft.AspNetCore.Http.HttpResults;

namespace TheAppManager.Sample.Forecast;

public static class WeatherForecastEndpoints
{
    public static void MapGetWeatherForecasts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/weatherforecast", Handler)
            .WithName("GetWeatherForecast");
    }

    private static Ok<WeatherForecast[]> Handler(WeatherForecastService service)
        => TypedResults.Ok(service.GetForecasts());
}
