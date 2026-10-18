using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager.Testing;

/// <summary>
/// A test host that builds a TestServer from registered modules.
/// Provides a fluent API for integration testing with IAppModule implementations.
/// </summary>
public sealed class AppModuleTestHost : IAsyncDisposable
{
    private readonly AppModuleCollection _modules = new();
    private readonly List<Action<WebApplicationBuilder>> _builderConfigurations = [];
    private WebApplication? _app;
    private HttpClient? _client;

    /// <summary>
    /// Adds a module by type.
    /// </summary>
    public AppModuleTestHost Add<TModule>() where TModule : IAppModule, new()
    {
        _modules.Add<TModule>();
        return this;
    }

    /// <summary>
    /// Adds an existing module instance.
    /// </summary>
    public AppModuleTestHost Add(IAppModule module)
    {
        _modules.Add(module);
        return this;
    }

    /// <summary>
    /// Adds a module applied only when <paramref name="predicate"/> returns true for the real builder, evaluated once at start.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
    public AppModuleTestHost AddIf<TModule>(Func<IHostApplicationBuilder, bool> predicate) where TModule : IAppModule, new()
    {
        _modules.AddIf<TModule>(predicate);
        return this;
    }

    /// <summary>
    /// Replaces the first module of type <typeparamref name="TOld"/> with a new <typeparamref name="TNew"/>, keeping its position.
    /// </summary>
    /// <exception cref="InvalidOperationException">No module of type <typeparamref name="TOld"/> was added.</exception>
    public AppModuleTestHost Replace<TOld, TNew>()
        where TOld : IAppModule
        where TNew : IAppModule, new()
    {
        _modules.Replace<TOld, TNew>();
        return this;
    }

    /// <summary>
    /// Configures the WebApplicationBuilder before modules are applied.
    /// </summary>
    public AppModuleTestHost ConfigureBuilder(Action<WebApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _builderConfigurations.Add(configure);
        return this;
    }

    /// <summary>
    /// Builds and starts the test host.
    /// </summary>
    public async Task<AppModuleTestHost> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        foreach (var configure in _builderConfigurations)
        {
            configure(builder);
        }

        _app = ModulePipeline.Build(builder, _modules);

        await _app.StartAsync();
        return this;
    }

    /// <summary>
    /// Gets an HttpClient configured to send requests to the test server.
    /// </summary>
    public HttpClient GetTestClient()
    {
        if (_app is null)
            throw new InvalidOperationException("Call StartAsync() before getting a test client.");

        _client ??= _app.GetTestClient();
        return _client;
    }

    /// <summary>
    /// Gets a service from the test host's dependency injection container.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull
    {
        if (_app is null)
            throw new InvalidOperationException("Call StartAsync() before resolving services.");

        return _app.Services.GetRequiredService<T>();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
