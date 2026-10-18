using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// Compiled mirrors of the README "patterns" section (checked by DocSnippetsTests).
namespace TheAppManager.DocsSnippets.Readme.Patterns;

// begin-snippet: readme-validate
public sealed class WeatherModule : IWebModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddOptions<WeatherOptions>()
            .Bind(builder.Configuration.GetSection("Weather"))
            .Validate(o => o.Days is > 0 and <= 14, "Weather:Days must be between 1 and 14.")
            .ValidateOnStart();
}
// end-snippet

// begin-snippet: readme-bundle
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
// end-snippet

// begin-snippet: readme-tryadd
public sealed class EmailModule : IAppModule
{
    public void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
}
// end-snippet

// Stubs the snippets reference.
public sealed class WeatherOptions
{
    public int Days { get; set; } = 5;
}

public sealed class InvoiceService;
public sealed class TaxCalculator;
public interface IEmailSender;
public sealed class SmtpEmailSender : IEmailSender;
