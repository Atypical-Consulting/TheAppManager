using Microsoft.Extensions.Options;
using Spectre.Console;

namespace TheAppManager.Sample.Console;

public sealed class ForecastOptions
{
    public int Days { get; set; } = 5;
}

/// <summary>Renders a few days of random forecasts as a Spectre.Console table.</summary>
public sealed class ForecastTable(IOptions<ForecastOptions> options)
{
    private static readonly string[] Summaries = ["Freezing", "Chilly", "Mild", "Warm", "Hot"];

    public void Render()
    {
        var table = new Table().AddColumns("Date", "Temp (C)", "Summary");
        for (var day = 1; day <= options.Value.Days; day++)
        {
            table.AddRow(
                DateOnly.FromDateTime(DateTime.Today.AddDays(day)).ToString("yyyy-MM-dd"),
                Random.Shared.Next(-20, 55).ToString(),
                Summaries[Random.Shared.Next(Summaries.Length)]);
        }

        AnsiConsole.Write(table);
    }
}
