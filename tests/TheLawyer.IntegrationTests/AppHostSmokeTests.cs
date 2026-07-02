using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TheLawyer.IntegrationTests;

/// <summary>
/// Foundations-epic runtime guard: boots the whole Aspire AppHost (real Postgres + Redis
/// containers, real service discovery, real connection strings) and asserts the API's
/// cross-cutting surface actually serves. This is the regression test that fails if the
/// AppHost composition breaks (e.g. a missing hosting package) or the API stops booting.
/// </summary>
public sealed class AppHostSmokeTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private static async Task<DistributedApplication> StartAppHostAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TheLawyer_AppHost>();

        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        var app = await builder.BuildAsync();
        await app.StartAsync();

        // StartAsync does not wait for readiness — wait for the API explicitly.
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications
            .WaitForResourceAsync("api", KnownResourceStates.Running)
            .WaitAsync(StartTimeout);

        return app;
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        await using var app = await StartAppHostAsync();
        using var client = app.CreateHttpClient("api", "http");
        client.Timeout = RequestTimeout;

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Ping_endpoint_returns_service_identity()
    {
        await using var app = await StartAppHostAsync();
        using var client = app.CreateHttpClient("api", "http");
        client.Timeout = RequestTimeout;

        var response = await client.GetAsync("/api/v1/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PingResponse>();
        Assert.NotNull(payload);
        Assert.Equal("TheLawyer.Api", payload!.Service);
    }

    private sealed record PingResponse(string Service, DateTimeOffset UtcNow);
}
