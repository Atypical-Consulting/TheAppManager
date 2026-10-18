![TheAppManager banner](https://raw.githubusercontent.com/Atypical-Consulting/TheAppManager/dev/.github/banner.png)

<p align="center">
  <img src="https://raw.githubusercontent.com/Atypical-Consulting/TheAppManager/dev/logo.png" alt="TheAppManager" width="128" />
</p>

# TheAppManager

> Compose `Program.cs` from ordered, testable modules — extensions on the builder you already have, not a replacement for it.

[![CI](https://github.com/Atypical-Consulting/TheAppManager/actions/workflows/ci.yml/badge.svg)](https://github.com/Atypical-Consulting/TheAppManager/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/TheAppManager.svg)](https://www.nuget.org/packages/TheAppManager)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Table of Contents

- [Why modules?](#why-modules)
- [Installation](#installation)
- [Quick start](#quick-start)
- [`IAppModule` vs `IWebModule`](#iappmodule-vs-iwebmodule)
- [Lifecycle](#lifecycle)
- [Composing modules](#composing-modules)
- [Assembly scanning (opt-in)](#assembly-scanning-opt-in)
- [Hosting models](#hosting-models)
- [Testing](#testing)
- [Patterns the framework already gives you](#patterns-the-framework-already-gives-you)
- [Out of scope](#out-of-scope)
- [Samples](#samples)
- [Migrating from v2](#migrating-from-v2)
- [Contributing](#contributing)
- [License](#license)

## Why modules?

A feature touches startup in up to three places: services, middleware and endpoints. TheAppManager puts
the three in one class, a **module**, and composes modules in one visible, ordered list. The list is
plain data, so the same composition runs in production and in tests, and a test can swap any module.

There is no new host and no container wrapper: `AddModules`, `UseModules` and `MapModules` are extension
methods on the ASP.NET Core and Generic Host builders. Everything you know about `Program.cs` still applies.

## Installation

```bash
dotnet add package TheAppManager
dotnet add package TheAppManager.Testing   # in test projects
```

Every type lives in the `TheAppManager` namespace (`TheAppManager.Testing` for the test helpers). The
core library targets .NET 10 and is trimming and Native AOT compatible, except for assembly scanning.

## Quick start

Write modules. Each hook is optional, so a module implements only what it needs:

<!-- snippet: readme-modules -->
```csharp
public sealed class ErrorHandlingModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddProblemDetails();

    public void ConfigureMiddleware(WebApplication app)
        => app.UseExceptionHandler();
}

public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddSingleton<WeatherService>();

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("/weatherforecast", (WeatherService weather) => weather.Get());
}
```

Compose them in `Program.cs`:

<!-- snippet: readme-program -->
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddModules(m => m
    .Add<ErrorHandlingModule>()
    .Add<WeatherModule>());

var app = builder.Build();
app.UseModules();
app.MapModules();
app.Run();
```

For the simple web case, `AppManager.RunAsync` does the same in one call:

<!-- snippet: readme-runasync -->
```csharp
await AppManager.RunAsync(args, m => m
    .Add<ErrorHandlingModule>()
    .Add<WeatherModule>());
```

## `IAppModule` vs `IWebModule`

| Interface | Hooks | Called by |
|---|---|---|
| `IAppModule` | `ConfigureServices(IHostApplicationBuilder)` | `builder.AddModules(...)` on any host: web, worker, console |
| `IWebModule : IAppModule` | adds `ConfigureMiddleware(WebApplication)` and `ConfigureEndpoints(IEndpointRouteBuilder)` | `app.UseModules()` and `app.MapModules()` on a web app |

A worker or console host only runs `ConfigureServices`. `UseModules` and `MapModules` skip modules that are
not `IWebModule`. Every hook has a default no-op implementation.

## Lifecycle

Modules do not run end to end one after the other. Startup runs in four phases, and each phase walks the
modules **in registration order**:

```text
builder.AddModules(...)   ConfigureServices    A → B → C
builder.Build()           (the container is built)
app.UseModules()          ConfigureMiddleware  A → B → C   (IWebModule only)
app.MapModules()          ConfigureEndpoints   A → B → C   (IWebModule only)
```

So module A's middleware runs before module B's for every request, including requests to B's endpoints.
**Register exception-handling and authentication/authorization modules first: their middleware must run
before any module's endpoints.**

- `AddModules` resolves the plan once: `AddIf` predicates are evaluated a single time against the real
  builder, so a module can never be active in one phase and missing from another.
- The resolved plan (each module and whether it is applied) is logged at `Information` when the host
  starts, and is available from DI as `ModulePlan`.
- `AddModules`, `UseModules` and `MapModules` each run once per app; a second call throws.
- A module instance belongs to one host. Do not share an instance between hosts (for example two test
  hosts); register the type, or create a new instance per host.

## Composing modules

Name the composition once, in a static method, so `Program.cs` and the tests use the same list:

<!-- snippet: readme-composition -->
```csharp
public static class AppModules
{
    public static void Configure(AppModuleCollection modules) => modules
        .Add<ErrorHandlingModule>()
        .Add<AuthModule>()
        .AddIf<SwaggerModule>(b => b.Environment.IsDevelopment())
        .AddIf<MetricsModule>(b => b.Configuration.GetValue<bool>("Features:Metrics"))
        .Add<WeatherModule>();
}
```

`Program.cs` then calls `builder.AddModules(AppModules.Configure)`.

- **`AddIf`** takes a predicate on the `IHostApplicationBuilder`, so a condition can read the environment
  or configuration.
- **`Replace<TOld, TNew>()`** swaps a module type and keeps its position and its predicate. `AddModules`
  also accepts a prebuilt collection:

  <!-- snippet: readme-replace -->
  ```csharp
  var modules = new AppModuleCollection();
  AppModules.Configure(modules);
  modules.Replace<WeatherModule, CachedWeatherModule>();

  builder.AddModules(modules);
  ```

- **Duplicates throw.** A module type registered twice is almost always a mistake, so `AddModules` throws.
  Mark the class `[AllowMultipleInstances]` when several instances are intended, then add instances with
  `m.Add(new HealthCheckModule("db")).Add(new HealthCheckModule("cache"))`:

  <!-- snippet: readme-multiple -->
  ```csharp
  [AllowMultipleInstances]
  public sealed class HealthCheckModule(string name) : IAppModule
  {
      public void ConfigureServices(IHostApplicationBuilder builder)
          => builder.Services.AddHealthChecks().AddCheck(name, () => HealthCheckResult.Healthy());
  }
  ```

## Assembly scanning (opt-in)

Explicit registration is the default and the recommended path. To pick up every module of an assembly:

<!-- snippet: readme-discovery -->
```csharp
builder.AddModules(m => m.AddFromAssemblyOf<Program>());
```

- Scanned modules are added in ordinal order of their full type name, not in a meaningful order. Prefer
  explicit registration when middleware order matters.
- Scanning finds non-abstract, non-generic classes with a public parameterless constructor. Mark a
  type `[ExcludeFromDiscovery]` to skip it.
- It throws when the assembly contains no module, so a wrong anchor type fails loudly.
- `AddFromAssembly` and `AddFromAssemblyOf` are marked `[RequiresUnreferencedCode]`: reflection over an
  assembly is not trimming or Native AOT safe. Everything else in the library is.

## Hosting models

**Worker and console apps** use the Generic Host and `IAppModule`. Spectre.Console apps take the same path:

<!-- snippet: readme-worker-module -->
```csharp
public sealed class OrderProcessingModule : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddHostedService<OrderProcessor>();
}
```

<!-- snippet: readme-worker -->
```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddModules(m => m
    .Add<MessagingModule>()
    .Add<OrderProcessingModule>());

await builder.Build().RunAsync();
```

**Aspire.** Call the service defaults on the same builder, before or after `AddModules`:

<!-- snippet: readme-aspire -->
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddModules(AppModules.Configure);

var app = builder.Build();
app.UseModules();
app.MapDefaultEndpoints();
app.MapModules();
app.Run();
```

**Mounting under a route group.** `MapModules` works on any `IEndpointRouteBuilder`, so every module's
endpoints can share a prefix and a policy:

<!-- snippet: readme-mapgroup -->
```csharp
app.UseModules();
app.MapGroup("/api").RequireAuthorization().MapModules();
```

## Testing

The `TheAppManager.Testing` package offers two ways in.

**Slice tests with `AppModuleTestHost`.** Start a subset of modules on a TestServer, exactly as production
hosting does (`AddModules` → `Build` → `UseModules` → `MapModules`). `ConfigureTestServices` runs after
every module's `ConfigureServices`, so a fake registered there wins:

<!-- snippet: readme-testhost -->
```csharp
await using var host = await new AppModuleTestHost(m => m.Add<WeatherModule>())
    .ConfigureTestServices(s => s.AddSingleton<IWeatherSource, FakeWeatherSource>())
    .StartAsync();

var response = await host.CreateClient().GetAsync("/weatherforecast");
response.EnsureSuccessStatusCode();
```

Pass the production composition to test all of it, without `Program.cs`:

<!-- snippet: readme-testhost-production -->
```csharp
await using var host = await new AppModuleTestHost(AppModules.Configure).StartAsync();
```

**The real app under `WebApplicationFactory`.** `WithModules` edits the composition of `Program` after its
own `AddModules` call and before the plan is resolved:

<!-- snippet: readme-waf -->
```csharp
await using var factory = new WebApplicationFactory<Program>()
    .WithModules(m => m
        .Replace<WeatherModule, FakeWeatherModule>()
        .Without<SwaggerModule>());

var response = await factory.CreateClient().GetAsync("/api/weatherforecast");
response.EnsureSuccessStatusCode();
```

`Program` must pass `args` to `WebApplication.CreateBuilder(args)`: the amendment reaches the app through
them, and building the host throws when it was not applied. Chained `WithModules` calls apply in call order.
To swap a single service rather than a module, use `WebApplicationFactory`'s own `ConfigureTestServices`.

## Patterns the framework already gives you

TheAppManager stays small because ASP.NET Core already covers these. Use them inside modules.

- **Route groups**: `MapGroup` (see [Mounting under a route group](#hosting-models)), or a group inside one
  module's `ConfigureEndpoints`.
- **Options validation**: fail fast on bad configuration with `ValidateOnStart`, from `ConfigureServices`:

  <!-- snippet: readme-validate -->
  ```csharp
  public sealed class WeatherModule : IWebModule
  {
      public void ConfigureServices(IHostApplicationBuilder builder)
          => builder.Services.AddOptions<WeatherOptions>()
              .Bind(builder.Configuration.GetSection("Weather"))
              .Validate(o => o.Days is > 0 and <= 14, "Weather:Days must be between 1 and 14.")
              .ValidateOnStart();
  }
  ```

- **Modules of modules**: group registrations in an extension method and call it from a module. The
  extension stays usable without TheAppManager:

  <!-- snippet: readme-bundle -->
  ```csharp
  public static class BillingServiceCollectionExtensions
  {
      public static IServiceCollection AddBilling(this IServiceCollection services)
          => services
              .AddSingleton<InvoiceService>()
              .AddSingleton<TaxCalculator>();
  }

  public sealed class BillingModule : IAppModule
  {
      public void ConfigureServices(IHostApplicationBuilder builder)
          => builder.Services.AddBilling();
  }
  ```

- **Overridable defaults**: when two modules register the same service, the last registration wins and
  nothing detects it. A module that only provides a default should use `TryAdd*`, so a module registered
  before it (or `Program.cs`) can supply its own implementation:

  <!-- snippet: readme-tryadd -->
  ```csharp
  public sealed class EmailModule : IAppModule
  {
      public void ConfigureServices(IHostApplicationBuilder builder)
          => builder.Services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
  }
  ```

## Out of scope

These are deliberate non-goals, written down so they are not re-debated:

- **Async hooks.** Startup hooks stay synchronous. Use `IHostedService` or `app.Lifetime` for async work.
- **Multi-tenancy.** It is an application concern; frameworks such as OrchardCore and ABP cover it.
- **Per-phase ordering.** One order for every phase keeps the plan readable. If a module needs its
  middleware elsewhere, split it into two modules.
- **Modules resolved from DI.** Modules configure the container, so they run before it exists.
  Use constructor parameters and `Add(instance)` instead.
- **`DependsOn` and automatic ordering.** The registration order is the order; an explicit list beats a
  dependency graph you have to solve in your head.

## Samples

- [`samples/TheAppManager.Sample`](samples/TheAppManager.Sample): web app with a named composition,
  `MapGroup("/api").RequireAuthorization()` and Aspire service defaults.
- [`samples/TheAppManager.Sample.Console`](samples/TheAppManager.Sample.Console): Spectre.Console app on
  the Generic Host.
- [`samples/TheAppManager.Sample.AppHost`](samples/TheAppManager.Sample.AppHost): Aspire AppHost.
- [`samples/TheAppManager.Sample.Tests`](samples/TheAppManager.Sample.Tests): slice tests and
  `WebApplicationFactory` tests of the sample.

## Migrating from v2

v3 is a breaking release. See the [v2 to v3 migration guide](docs/migration-v2-to-v3.md) for a mapping of
every removed or renamed member.

## Project structure

```text
src/TheAppManager/            → core package
src/TheAppManager.Testing/    → test host and WebApplicationFactory helpers
samples/                      → web, console, Aspire and sample tests
tests/                        → unit, integration, AOT smoke and documentation snippet tests
```

<!-- portfolio-techstack:start -->

## Tech Stack

- **C#** / .NET 10
- ASP.NET Core and the .NET Generic Host
- Microsoft.AspNetCore.TestHost and Microsoft.AspNetCore.Mvc.Testing (`TheAppManager.Testing` only)
- Swashbuckle.AspNetCore, Spectre.Console and Aspire (samples)
- Shouldly
- xunit
- xunit.runner.visualstudio

<!-- portfolio-techstack:end -->

## Roadmap

- [ ] Health-check integration for modules that expose their own health endpoints
- [ ] Source-generator based discovery to replace runtime assembly scanning
- [ ] More samples (Blazor Server)

See the [open issues](https://github.com/Atypical-Consulting/TheAppManager/issues) for details and to propose new ideas.

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md): pull requests target `dev`, and commit
messages follow Conventional Commits. Every C# block in this README is mirrored by a compiled snippet in
`tests/TheAppManager.DocsSnippets`, and a test fails when they drift apart.

## License

[MIT](LICENSE)

<!-- portfolio-nugetkeep:start -->
---
Built by [Atypical Consulting](https://www.atypical.consulting). We also make
[NuGetKeep](https://nugetkeep.com/?utm_source=github-readme&utm_medium=readme&utm_campaign=launch-2026-07),
a self-hosted NuGet server with supply-chain quarantine.
<!-- portfolio-nugetkeep:end -->
