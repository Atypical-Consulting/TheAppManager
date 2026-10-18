using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TheAppManager.Tests;

public class AppManagerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly string[] Args = ["--urls", "http://127.0.0.1:0"];

    [Fact]
    public async Task RunAsync_CompletesWhenApplicationStops()
    {
        await AppManager.RunAsync(Args, m => m.Add<StopOnStartModule>()).WaitAsync(Timeout);
    }

    [Fact]
    public async Task RunAsync_CompletesWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        var run = AppManager.RunAsync(Args, _ => { }, cts.Token);
        await cts.CancelAsync();

        // A token cancelled before the host has started surfaces as OperationCanceledException, as with IHost.RunAsync.
        var ex = await Record.ExceptionAsync(() => run.WaitAsync(Timeout));
        (ex is null or OperationCanceledException).ShouldBeTrue(ex?.ToString());
    }

    [Fact]
    public void RunAsync_NullArguments_ThrowSynchronously()
    {
        Should.Throw<ArgumentNullException>(() => AppManager.RunAsync(null!, _ => { }));
        Should.Throw<ArgumentNullException>(() => AppManager.RunAsync(Args, null!));
    }

    [Fact]
    public async Task RunAsync_MiddlewareFailure_DisposesTheAppAndRethrows()
    {
        var module = new FailingMiddlewareModule();

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => AppManager.RunAsync(Args, m => m.Add(module)).WaitAsync(Timeout));

        ex.Message.ShouldBe(FailingMiddlewareModule.Message);
        module.Probe.ShouldNotBeNull().Disposed.ShouldBeTrue();
    }

    private sealed class StopOnStartModule : IWebModule
    {
        public void ConfigureMiddleware(WebApplication app)
            => app.Lifetime.ApplicationStarted.Register(app.Lifetime.StopApplication);
    }

    private sealed class FailingMiddlewareModule : IWebModule
    {
        public const string Message = "middleware failed";

        public DisposableProbe? Probe { get; private set; }

        public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddSingleton<DisposableProbe>();

        public void ConfigureMiddleware(WebApplication app)
        {
            Probe = app.Services.GetRequiredService<DisposableProbe>();
            throw new InvalidOperationException(Message);
        }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
