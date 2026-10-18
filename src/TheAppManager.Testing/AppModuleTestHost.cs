using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager.Testing;

/// <summary>
/// Builds and starts a TestServer-backed app from a module composition, the same way production hosting does
/// (<c>AddModules</c> → <c>Build</c> → <c>UseModules</c> → <c>MapModules</c>).
/// </summary>
public sealed class AppModuleTestHost : IAsyncDisposable
{
    private readonly Action<WebApplicationBuilder> _addModules;
    private readonly WebApplicationOptions? _options;
    private readonly List<Action<IServiceCollection>> _testServices = [];
    private WebApplication? _app;
    private bool _started;

    /// <summary>Creates a host for a prebuilt composition, e.g. the one production hosting uses.</summary>
    /// <param name="modules">The module composition to host.</param>
    /// <param name="options">Builder options (arguments, environment, application name, content root).</param>
    /// <exception cref="ArgumentNullException"><paramref name="modules"/> is null.</exception>
    public AppModuleTestHost(AppModuleCollection modules, WebApplicationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _addModules = builder => builder.AddModules(modules);
        _options = options;
    }

    /// <summary>Creates a host for the composition built by <paramref name="configure"/>.</summary>
    /// <param name="configure">Builds the module composition, e.g. <c>m => m.Add&lt;MyModule&gt;()</c>.</param>
    /// <param name="options">Builder options (arguments, environment, application name, content root).</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    public AppModuleTestHost(Action<AppModuleCollection> configure, WebApplicationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _addModules = builder => builder.AddModules(configure);
        _options = options;
    }

    /// <summary>
    /// Registers a service override applied after every module's <c>ConfigureServices</c>, before <c>Build</c>,
    /// so a fake registered here wins over the module's registration.
    /// </summary>
    /// <param name="configure">Edits the service collection.</param>
    /// <returns>This host.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><see cref="StartAsync"/> was already called.</exception>
    public AppModuleTestHost ConfigureTestServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_started)
            throw new InvalidOperationException("ConfigureTestServices must be called before StartAsync().");

        _testServices.Add(configure);
        return this;
    }

    /// <summary>The started app's root service provider.</summary>
    /// <exception cref="InvalidOperationException">The host has not been started.</exception>
    public IServiceProvider Services => StartedApp.Services;

    /// <summary>
    /// Builds and starts the app on a TestServer. The environment defaults to <c>Development</c>, and the
    /// container validates scopes and the graph on build in every environment. The host is single-use: if any
    /// phase after <c>Build</c> fails, the app is disposed and the exception rethrown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>This host, started.</returns>
    /// <exception cref="InvalidOperationException"><see cref="StartAsync"/> was already called, even if it failed.</exception>
    public async Task<AppModuleTestHost> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            throw new InvalidOperationException("AppModuleTestHost is single-use; create a new instance.");
        _started = true;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = _options?.Args,
            ApplicationName = _options?.ApplicationName,
            ContentRootPath = _options?.ContentRootPath,
            WebRootPath = _options?.WebRootPath,
            EnvironmentName = _options?.EnvironmentName ?? Environments.Development,
        });
        builder.WebHost.UseTestServer();
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });
        _addModules(builder);

        foreach (var configure in _testServices)
        {
            configure(builder.Services);
        }

        var app = builder.Build();
        try
        {
            app.UseModules();
            app.MapModules();
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        _app = app;
        return this;
    }

    /// <summary>Creates a new client for the TestServer; the caller owns and disposes it.</summary>
    /// <exception cref="InvalidOperationException">The host has not been started.</exception>
    public HttpClient CreateClient() => StartedApp.GetTestClient();

    /// <summary>Stops and disposes the app, if one was started. Safe to call more than once.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is not { } app)
            return;

        _app = null;
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private WebApplication StartedApp => _app ?? throw new InvalidOperationException("Call StartAsync() first.");
}
