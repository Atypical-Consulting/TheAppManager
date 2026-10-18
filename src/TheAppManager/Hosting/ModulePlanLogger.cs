using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TheAppManager;

/// <summary>
/// Logs the <see cref="ModulePlan"/> when the host starts. Registered before the modules' own services,
/// so it starts, and logs, first on every host type.
/// </summary>
internal sealed partial class ModulePlanLogger(ModulePlan plan, ILoggerFactory loggerFactory) : IHostedService
{
    private readonly ILogger _logger = loggerFactory.CreateLogger("TheAppManager");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var active = plan.Where(e => e.Applied).Select(e => e.ModuleType.FullName).ToList();
        var skipped = plan.Where(e => !e.Applied).Select(e => e.ModuleType.FullName).ToList();

        LogPlan(_logger, active.Count, string.Join(", ", active), skipped.Count, string.Join(", ", skipped));
        LogTimings(_logger, plan.ServicesElapsed.TotalMilliseconds, plan.MiddlewareElapsed.TotalMilliseconds,
            plan.EndpointsElapsed.TotalMilliseconds);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Module plan: {ActiveCount} active [{ActiveModules}]; {SkippedCount} skipped [{SkippedModules}]")]
    private static partial void LogPlan(ILogger logger, int activeCount, string activeModules, int skippedCount, string skippedModules);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Module phases: services {ServicesMs} ms, middleware {MiddlewareMs} ms, endpoints {EndpointsMs} ms")]
    private static partial void LogTimings(ILogger logger, double servicesMs, double middlewareMs, double endpointsMs);
}
