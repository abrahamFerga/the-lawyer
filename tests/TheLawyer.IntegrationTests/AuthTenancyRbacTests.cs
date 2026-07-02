using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace TheLawyer.IntegrationTests;

/// <summary>
/// Feature #11 acceptance criteria, proven against the REAL stack (Aspire AppHost, real Postgres):
/// anonymous requests are rejected; a valid token resolves tenant + user + role; policies bound
/// from Rbac:RoleAssignments enforce separation of duties (attorney cannot approve invoices) and
/// the bookkeeper privilege boundary (trust yes, matter content no); and the EF global tenant
/// filter makes one tenant's rows invisible to another. One AppHost boot shared across tests.
/// </summary>
public sealed class AuthTenancyRbacTests : IClassFixture<AuthTenancyRbacTests.AppFixture>
{
    // Must match src/TheLawyer.Api/appsettings.Development.json DevJwt:SigningKey (dev fixture, not a secret).
    private const string DevSigningKey = "thelawyer-development-signing-key-not-a-secret-0123456789abcdef";

    private readonly AppFixture _fixture;

    public AuthTenancyRbacTests(AppFixture fixture) => _fixture = fixture;

    public sealed class AppFixture : IAsyncLifetime
    {
        public DistributedApplication App { get; private set; } = default!;

        public async Task InitializeAsync()
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TheLawyer_AppHost>();
            builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
            App = await builder.BuildAsync();
            await App.StartAsync();
            await App.Services.GetRequiredService<ResourceNotificationService>()
                .WaitForResourceAsync("api", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(120));
        }

        public async Task DisposeAsync() => await App.DisposeAsync();
    }

    private HttpClient Client()
    {
        var client = _fixture.App.CreateHttpClient("api", "http");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string Token(Guid tenantId, string role, Guid? userId = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevSigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("sub", (userId ?? Guid.NewGuid()).ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("role", role),
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient ClientAs(Guid tenantId, string role, Guid? userId = null)
    {
        var client = Client();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(tenantId, role, userId));
        return client;
    }

    [Fact]
    public async Task Anonymous_request_to_a_probe_is_rejected()
    {
        using var client = Client();
        var response = await client.GetAsync("/api/v1/probes/whoami");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_token_resolves_tenant_user_and_role()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var client = ClientAs(tenantId, "attorney", userId);

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/v1/probes/whoami");

        Assert.Equal(tenantId, payload.GetProperty("tenantId").GetGuid());
        Assert.Equal(userId, payload.GetProperty("userId").GetGuid());
        Assert.Equal("attorney", payload.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Separation_of_duties_attorney_cannot_approve_invoices()
    {
        var tenantId = Guid.NewGuid();

        using (var attorney = ClientAs(tenantId, "attorney"))
        {
            Assert.Equal(HttpStatusCode.OK, (await attorney.GetAsync("/api/v1/probes/matters-view")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await attorney.GetAsync("/api/v1/probes/invoices-approve")).StatusCode);
        }

        using var firmAdmin = ClientAs(tenantId, "firm-admin");
        Assert.Equal(HttpStatusCode.OK, (await firmAdmin.GetAsync("/api/v1/probes/invoices-approve")).StatusCode);
    }

    [Fact]
    public async Task Bookkeeper_privilege_boundary_trust_yes_matter_content_no()
    {
        using var bookkeeper = ClientAs(Guid.NewGuid(), "bookkeeper");

        Assert.Equal(HttpStatusCode.OK, (await bookkeeper.GetAsync("/api/v1/probes/trust-post")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bookkeeper.GetAsync("/api/v1/probes/matters-view")).StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_rows_are_invisible()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var email = $"iso-{Guid.NewGuid():N}@example.test";

        using (var adminA = ClientAs(tenantA, "firm-admin"))
        {
            var created = await adminA.PostAsJsonAsync("/api/v1/probes/users",
                new { email, fullName = "Isolation Probe" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var mine = await adminA.GetFromJsonAsync<JsonElement>("/api/v1/probes/users");
            Assert.Contains(mine.EnumerateArray(), u => u.GetProperty("email").GetString() == email);
        }

        // The other tenant sees NOTHING of tenant A — the global query filter, not endpoint logic.
        using var adminB = ClientAs(tenantB, "firm-admin");
        var theirs = await adminB.GetFromJsonAsync<JsonElement>("/api/v1/probes/users");
        Assert.DoesNotContain(theirs.EnumerateArray(), u => u.GetProperty("email").GetString() == email);
    }
}
