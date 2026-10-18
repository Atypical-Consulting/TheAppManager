using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Compiled mirrors of the README hosting section (checked by DocSnippetsTests).
namespace TheAppManager.DocsSnippets.Readme.Hosting;

// begin-snippet: readme-worker-module
public sealed class OrderProcessingModule : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddHostedService<OrderProcessor>();
}
// end-snippet

public static class Examples
{
    public static async Task Worker(string[] args)
    {
        // begin-snippet: readme-worker
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddModules(m => m
            .Add<MessagingModule>()
            .Add<OrderProcessingModule>());

        await builder.Build().RunAsync();
        // end-snippet
    }

    public static void Aspire(string[] args)
    {
        // begin-snippet: readme-aspire
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.AddModules(AppModules.Configure);

        var app = builder.Build();
        app.UseModules();
        app.MapDefaultEndpoints();
        app.MapModules();
        app.Run();
        // end-snippet
    }

    public static void Group(WebApplication app)
    {
        // begin-snippet: readme-mapgroup
        app.UseModules();
        app.MapGroup("/api").RequireAuthorization().MapModules();
        // end-snippet
    }
}

// Stubs the snippets reference (Aspire's ServiceDefaults project provides the real extensions).
public static class AppModules
{
    public static void Configure(AppModuleCollection modules) => modules.Add<MessagingModule>();
}

public static class ServiceDefaultsStub
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder => builder;

    public static WebApplication MapDefaultEndpoints(this WebApplication app) => app;
}

public sealed class MessagingModule : IAppModule;

public sealed class OrderProcessor : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
