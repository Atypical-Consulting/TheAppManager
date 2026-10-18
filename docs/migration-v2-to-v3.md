# Migrating from v2 to v3

v3 turns TheAppManager from a host that replaces `Program.cs` into a set of extensions on the builder you
already have. It is a clean break: there are no shims and no `[Obsolete]` members, because v2 had no real
installed base. Most apps migrate in a few minutes by following the table below.

## At a glance

| v2 | v3 | Note |
|---|---|---|
| `using TheAppManager.Modules;` / `TheAppManager.Startup;` / `TheAppManager.Testing;` | `using TheAppManager;` (+ `using TheAppManager.Testing;` in tests) | Every core type lives in the `TheAppManager` namespace. |
| `AppManager.Start(args, m => …)` / `AppManager.StartAsync(args, m => …)` | `await AppManager.RunAsync(args, m => …)` | One async entry point, with an optional `CancellationToken`. |
| `AppManager.Start(args)` (implicit discovery) | `AppManager.RunAsync(args, m => m.AddFromAssemblyOf<Program>())` | Discovery is opt-in and explicit. |
| `configureBuilder:` argument / `AppManagerBuilder.ConfigureBuilder(...)` | Configure the builder in `Program.cs` before `AddModules` | You own the builder again. |
| `new AppManagerBuilder(args).Build(modules)` + `AppManager.Run()` / `RunAsync()` | `WebApplication.CreateBuilder(args)` + `AddModules` / `UseModules` / `MapModules` | Works on any `IHostApplicationBuilder`. |
| `IAppModule.ConfigureServices(WebApplicationBuilder)` | `IAppModule.ConfigureServices(IHostApplicationBuilder)` | Use `builder.Services`, `builder.Configuration`, `builder.Environment`. `builder.WebHost` settings move to `Program.cs`. |
| `IAppModule.ConfigureMiddleware` / `ConfigureEndpoints` | Implement `IWebModule` | `IAppModule` keeps only `ConfigureServices`, so worker and console hosts can use it. |
| `AddIf<T>(bool condition)` | `AddIf<T>(_ => condition)` | Prefer a predicate on the builder (environment, configuration). |
| `AddIf<T>(Func<WebApplicationBuilder, bool>)` | `AddIf<T>(Func<IHostApplicationBuilder, bool>)` | Same semantics, evaluated once against the real builder. |
| `AddFromAssemblyOf<TModule>() where TModule : IAppModule` | `AddFromAssemblyOf<TAnchor>()` | Any type can anchor the scan. It throws when no module is found and is marked `[RequiresUnreferencedCode]`. |
| `ModuleDiscovery.DiscoverModules(assembly)` | `modules.AddFromAssembly(assembly)` | `ModuleDiscovery` is internal. Use `[ExcludeFromDiscovery]` to skip a type. |
| Adding the same module type twice silently ran it twice | Throws at `AddModules`, unless the class has `[AllowMultipleInstances]` | Catches accidental double registration. |
| `AppModuleTestHost` in the core package | `AppModuleTestHost` in the `TheAppManager.Testing` package | Production apps no longer ship `Microsoft.AspNetCore.TestHost`. |
| `new AppModuleTestHost().Add<X>()…` | `new AppModuleTestHost(m => m.Add<X>()…)` | The host takes a composition, so tests can reuse the one `Program.cs` uses. |
| `AppModuleTestHost.ConfigureBuilder(...)` | `ConfigureTestServices(...)` or `WebApplicationOptions` | Service overrides run after every module's `ConfigureServices`. |
| `GetTestClient()` / `GetRequiredService<T>()` | `CreateClient()` / `Services.GetRequiredService<T>()` | |
| No equivalent | `WebApplicationFactory<Program>.WithModules(m => m.Replace<…>().Without<…>())` | Swap or remove modules of the real app. |

## Program.cs

v2 hid the builder behind `AppManager.Start`. In v3 you write the four phases yourself:

<!-- snippet: migration-program -->
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("custom-settings.json", optional: true); // was ConfigureBuilder
builder.AddModules(m => m
    .Add<ErrorHandlingModule>()
    .Add<WeatherModule>());

var app = builder.Build();
app.UseModules();
app.MapModules();
await app.RunAsync();
```

Or keep a one-liner for the simple web case:

<!-- snippet: migration-runasync -->
```csharp
await AppManager.RunAsync(args, m => m
    .Add<ErrorHandlingModule>()
    .Add<WeatherModule>());
```

v2's `AppManager.Start(args)` scanned the entry assembly implicitly. Ask for it explicitly:

<!-- snippet: migration-discovery -->
```csharp
await AppManager.RunAsync(args, m => m.AddFromAssemblyOf<Program>());
```

## Modules

A module with middleware or endpoints implements `IWebModule`, and `ConfigureServices` takes an
`IHostApplicationBuilder`:

<!-- snippet: migration-module -->
```csharp
public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddSingleton<WeatherService>();

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/weatherforecast", (WeatherService weather) => weather.Get());
}
```

`AddIf` takes a predicate on the builder instead of a `bool`:

<!-- snippet: migration-addif -->
```csharp
modules.AddIf<SwaggerModule>(b => b.Environment.IsDevelopment());
```

A module type registered twice now throws. Mark the class when several instances are intended:

<!-- snippet: migration-duplicates -->
```csharp
[AllowMultipleInstances]
public sealed class HealthCheckModule(string name) : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddHealthChecks().AddCheck(name, () => HealthCheckResult.Healthy());
}
```

<!-- snippet: migration-duplicates-use -->
```csharp
modules
    .Add(new HealthCheckModule("db"))
    .Add(new HealthCheckModule("cache"));
```

## Tests

Reference the `TheAppManager.Testing` package. The test host takes a composition, and its members follow
`WebApplicationFactory` naming:

<!-- snippet: migration-testhost -->
```csharp
await using var host = await new AppModuleTestHost(m => m
        .Add<WeatherModule>()
        .Replace<WeatherModule, FakeWeatherModule>())
    .ConfigureTestServices(s => s.AddSingleton(TimeProvider.System))
    .StartAsync();

var client = host.CreateClient();
var time = host.Services.GetRequiredService<TimeProvider>();
```

To test the real app with a module swapped or removed, use `WithModules` on `WebApplicationFactory`:

<!-- snippet: migration-waf -->
```csharp
await using var factory = new WebApplicationFactory<Program>()
    .WithModules(m => m
        .Replace<WeatherModule, FakeWeatherModule>()
        .Without<SwaggerModule>());

var client = factory.CreateClient();
```

`Program` must pass `args` to `WebApplication.CreateBuilder(args)`, because the amendment reaches the app
through them.
