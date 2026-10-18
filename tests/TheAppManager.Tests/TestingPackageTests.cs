using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TheAppManager.Testing;

namespace TheAppManager.Tests;

public class TestingPackageTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void CoreAssembly_DoesNotReferenceTestHost()
    {
        typeof(AppModuleCollection).Assembly.GetReferencedAssemblies()
            .ShouldNotContain(a => a.Name == "Microsoft.AspNetCore.TestHost");
    }

    [Fact]
    public void Host_LivesInTheTestingAssembly_AndIsSealed()
    {
        typeof(AppModuleTestHost).Assembly.GetName().Name.ShouldBe("TheAppManager.Testing");
        typeof(AppModuleTestHost).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void CoreAssembly_GrantsNoInternalsToTheTestingAssembly()
    {
        typeof(AppModuleCollection).Assembly.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
            .Cast<InternalsVisibleToAttribute>()
            .ShouldNotContain(a => a.AssemblyName.StartsWith("TheAppManager.Testing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfigureDelegate_HostsTheComposition()
    {
        await using var host = new AppModuleTestHost(m => m.Add<HelloModule>());

        (await GetHelloAsync(host)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PrebuiltCollection_HostsTheComposition()
    {
        await using var host = new AppModuleTestHost(new AppModuleCollection().Add<HelloModule>());

        (await GetHelloAsync(host)).ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpStatusCode> GetHelloAsync(AppModuleTestHost host)
    {
        await host.StartAsync().WaitAsync(Timeout);
        using var client = host.CreateClient();
        return (await client.GetAsync("/hello").WaitAsync(Timeout)).StatusCode;
    }

    private sealed class HelloModule : IWebModule
    {
        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/hello", () => "Hello");
    }
}
