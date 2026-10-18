using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace TheAppManager;

/// <summary>
/// One-line startup for the simple web case. For a builder you create yourself, a worker or a console host,
/// use <c>builder.AddModules(...)</c>, <c>app.UseModules()</c> and <c>app.MapModules()</c> directly.
/// </summary>
public static class AppManager
{
    /// <summary>
    /// Creates a web application from <paramref name="args"/>, applies the modules registered by
    /// <paramref name="configure"/> (<c>AddModules</c>, <c>UseModules</c>, <c>MapModules</c>) and runs it until
    /// it shuts down or <paramref name="cancellationToken"/> is cancelled. If a module fails while configuring
    /// middleware or endpoints, the application is disposed and the exception is rethrown.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="configure">Registers the modules.</param>
    /// <param name="cancellationToken">Stops the application when cancelled.</param>
    /// <returns>A task that completes when the application has shut down.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> or <paramref name="configure"/> is null.</exception>
    public static Task RunAsync(string[] args, Action<AppModuleCollection> configure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configure);

        return RunCoreAsync(args, configure, cancellationToken);
    }

    private static async Task RunCoreAsync(string[] args, Action<AppModuleCollection> configure,
        CancellationToken cancellationToken)
    {
        var app = WebApplication.CreateBuilder(args).AddModules(configure).Build();

        try
        {
            app.UseModules();
            app.MapModules();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        // Disposes the host once it has stopped.
        await ((IHost)app).RunAsync(cancellationToken);
    }
}
