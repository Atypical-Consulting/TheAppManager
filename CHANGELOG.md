# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [2.2.0](https://github.com/Atypical-Consulting/TheAppManager/compare/v2.1.0...v2.2.0) (2026-10-18)


### Features

* add AddIf overload with a builder-aware predicate ([#60](https://github.com/Atypical-Consulting/TheAppManager/issues/60)) ([#69](https://github.com/Atypical-Consulting/TheAppManager/issues/69)) ([46d01c2](https://github.com/Atypical-Consulting/TheAppManager/commit/46d01c2316dcceda3207c766f45795758d995c3e))
* add Replace to AppModuleTestHost ([#61](https://github.com/Atypical-Consulting/TheAppManager/issues/61)) ([#67](https://github.com/Atypical-Consulting/TheAppManager/issues/67)) ([7e999d2](https://github.com/Atypical-Consulting/TheAppManager/commit/7e999d26eb84a4bcc713ce813d15a2affc6fa8b4))


### Bug Fixes

* allow cancelling AppManager.RunAsync ([b21d21f](https://github.com/Atypical-Consulting/TheAppManager/commit/b21d21f14d1cb7329be33f2e51a6e3eedc22bc2f))
* do not register a module twice when scanning its assembly ([b21d21f](https://github.com/Atypical-Consulting/TheAppManager/commit/b21d21f14d1cb7329be33f2e51a6e3eedc22bc2f))
* keep discovering modules when some types fail to load ([b21d21f](https://github.com/Atypical-Consulting/TheAppManager/commit/b21d21f14d1cb7329be33f2e51a6e3eedc22bc2f))
* run every ConfigureBuilder callback instead of only the last ([b21d21f](https://github.com/Atypical-Consulting/TheAppManager/commit/b21d21f14d1cb7329be33f2e51a6e3eedc22bc2f)), closes [#62](https://github.com/Atypical-Consulting/TheAppManager/issues/62)


### Refactor

* share one module pipeline between AppManagerBuilder and AppModuleTestHost ([#59](https://github.com/Atypical-Consulting/TheAppManager/issues/59)) ([#64](https://github.com/Atypical-Consulting/TheAppManager/issues/64)) ([e7dab9f](https://github.com/Atypical-Consulting/TheAppManager/commit/e7dab9f020b4d94e577b54faceeaa3322ee0a471))

## [2.1.0](https://github.com/Atypical-Consulting/TheAppManager/compare/v2.0.0...v2.1.0) (2026-10-18)


### Features

* add AppManager.StartAsync and RunAsync for async application startup ([a5affa0](https://github.com/Atypical-Consulting/TheAppManager/commit/a5affa04c839dddcfa871f2bd1234d2d4f3246cd))

## [2.0.0] - 2026-03-09

### Added

- `IAppModule` interface with default method implementations — modules only override what they need
- `AppModuleCollection` for fluent module registration with `Add<T>()`, `Add(instance)`, `AddIf<T>(condition)`, `Replace<TOld, TNew>()`, `AddFromAssemblyOf<T>()`, `AddFromAssembly(assembly)`
- `ModuleDiscovery` for automatic assembly scanning of `IAppModule` implementations
- `AppModuleTestHost` for integration testing modules with TestServer
- Auto-discovery mode — call `AppManager.Start(args)` without a configure callback to discover modules automatically
- Registration-order execution — modules are applied in the order they are registered
- Renovate configuration for automated dependency updates

### Changed

- `AppManager.StartApplication()` renamed to `AppManager.Start()`
- `AppManager.StartApplicationAsync()` renamed to `AppManager.StartAsync()`
- `AppManagerBuilder.Build()` now takes `AppModuleCollection` instead of `IAppConfigurationStrategy`
- `ConfigureServices` now receives `WebApplicationBuilder` instead of `IServiceCollection`
- Restructured project into class library (`src/TheAppManager`), sample app (`samples/TheAppManager.Sample`), and test project (`tests/TheAppManager.Tests`)
- Upgraded target framework to .NET 10
- Updated CI actions: `actions/checkout` to v6, `actions/setup-dotnet` to v5, `actions/upload-artifact` to v6
- Updated `Swashbuckle.AspNetCore` to v10
- Updated `xunit.runner.visualstudio` to 3.1.5
- Updated `dotnet-sdk` to v10.0.103
- Updated README with architecture diagram, installation instructions, and usage examples

### Removed

- `IAppConfigurationStrategy` interface — replaced by `IAppModule`
- `DefaultAppConfiguration` class — no opinionated defaults; modules are inert by default

### Breaking Changes

- Complete API redesign — 0.1.0 code (`IAppConfigurationStrategy`, `DefaultAppConfiguration`) is not compatible; no migration guide was published.

## [0.1.0] - 2024-05-01

### Added

- Initial project setup with ASP.NET Core web application
- `IAppConfigurationStrategy` interface for defining custom startup configurations using the Strategy pattern
- `DefaultAppConfiguration` base implementation with HTTPS redirection and root endpoint
- `AppManager` static entry point with `StartApplication` and `StartApplicationAsync` methods
- `AppManagerBuilder` for advanced builder-level configuration
- Support for builder configuration hooks via `Action<WebApplicationBuilder>`
- Endpoint refactoring for cleaner route definitions
