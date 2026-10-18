# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

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

- Complete API redesign — v1.x code is not compatible. See README for migration guidance.

## [0.1.0] - 2024-05-01

### Added

- Initial project setup with ASP.NET Core web application
- `IAppConfigurationStrategy` interface for defining custom startup configurations using the Strategy pattern
- `DefaultAppConfiguration` base implementation with HTTPS redirection and root endpoint
- `AppManager` static entry point with `StartApplication` and `StartApplicationAsync` methods
- `AppManagerBuilder` for advanced builder-level configuration
- Support for builder configuration hooks via `Action<WebApplicationBuilder>`
- Endpoint refactoring for cleaner route definitions
