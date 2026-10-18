<img src="https://raw.githubusercontent.com/Atypical-Consulting/TheAppManager/dev/logo.png" alt="TheAppManager" width="96" />

# TheAppManager

Compose `Program.cs` from ordered, testable modules — extensions on the builder you already have, not a
replacement for it. A module groups the services, middleware and endpoints of one feature. `AddModules`,
`UseModules` and `MapModules` apply them to any ASP.NET Core, worker or console host, phase by phase, in
registration order. Trimming and Native AOT compatible (except the opt-in assembly scanning).

<!-- snippet: nuget-core-module -->
```csharp
public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddSingleton<WeatherService>();

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/weatherforecast", (WeatherService weather) => weather.Get());
}
```

<!-- snippet: nuget-core-program -->
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddModules(m => m.Add<WeatherModule>());

var app = builder.Build();
app.UseModules();
app.MapModules();
app.Run();
```

Test compositions with the [TheAppManager.Testing](https://www.nuget.org/packages/TheAppManager.Testing) package.

- [Documentation](https://github.com/Atypical-Consulting/TheAppManager#readme)
- [Migrating from v2](https://github.com/Atypical-Consulting/TheAppManager/blob/dev/docs/migration-v2-to-v3.md)
- [Changelog](https://github.com/Atypical-Consulting/TheAppManager/blob/dev/CHANGELOG.md)
