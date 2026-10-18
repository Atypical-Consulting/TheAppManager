<img src="https://raw.githubusercontent.com/Atypical-Consulting/TheAppManager/dev/logo.png" alt="TheAppManager" width="96" />

# TheAppManager.Testing

Test helpers for [TheAppManager](https://www.nuget.org/packages/TheAppManager) module compositions.

- `AppModuleTestHost` starts a subset of modules (or the whole production composition) on a TestServer,
  through the same `AddModules` → `Build` → `UseModules` → `MapModules` path as production.
- `WithModules` replaces or removes modules of the real app under `WebApplicationFactory<Program>`.

<!-- snippet: nuget-testing -->
```csharp
await using var host = await new AppModuleTestHost(m => m.Add<WeatherModule>())
    .ConfigureTestServices(s => s.AddSingleton<IWeatherSource, FakeWeatherSource>())
    .StartAsync();

var response = await host.CreateClient().GetAsync("/weatherforecast");

await using var factory = new WebApplicationFactory<Program>()
    .WithModules(m => m.Replace<WeatherModule, FakeWeatherModule>());
```

- [Documentation](https://github.com/Atypical-Consulting/TheAppManager#testing)
- [Migrating from v2](https://github.com/Atypical-Consulting/TheAppManager/blob/dev/docs/migration-v2-to-v3.md)
- [Changelog](https://github.com/Atypical-Consulting/TheAppManager/blob/dev/CHANGELOG.md)
