namespace TheAppManager.Sample.Modules;

/// <summary>Turns unhandled exceptions into ProblemDetails responses without leaking stack traces.</summary>
public sealed class ErrorHandlingModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder) => builder.Services.AddProblemDetails();

    public void ConfigureMiddleware(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
    }
}
