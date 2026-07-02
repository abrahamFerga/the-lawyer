using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TheLawyer.Api.Auth;
using TheLawyer.Api.Endpoints;
using TheLawyer.Api.Middleware;
using TheLawyer.Infrastructure;
using TheLawyer.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, health checks, resilient HTTP, service discovery.
builder.AddServiceDefaults();

// Infrastructure: EF Core, AmbientTenantContext, audit log abstractions.
var postgresConn = builder.Configuration.GetConnectionString("postgres")
    ?? throw new InvalidOperationException("Connection string 'postgres' missing. Aspire AppHost should supply it.");
builder.Services.AddTheLawyerInfrastructure(postgresConn);

// AuthN: JWT Bearer against Entra ID B2C. In Development ONLY, a symmetric DevJwt signing key
// (integration tests and local runs mint their own tokens with it) replaces the B2C authority —
// the key is a committed dev fixture, not a secret, and the branch is unreachable in production.
var devJwtKey = builder.Configuration["DevJwt:SigningKey"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(devJwtKey))
        {
            // Keep raw claim names (tenant_id, role, sub) — the default inbound map renames them
            // to SOAP-era URIs, which breaks the middleware and every RequireClaim("role", ...).
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(devJwtKey)),
                RoleClaimType = "role",
                NameClaimType = "sub",
            };
            return;
        }

        builder.Configuration.Bind("AzureAdB2C", options);
        // The tenant_id and role custom claims are extracted by TenantResolutionMiddleware.
    });

// AuthZ: every policy named <Module>.<Action>, bound from Rbac:RoleAssignments (config, not code).
builder.Services.AddTheLawyerAuthorization(builder.Configuration);

// Idempotency + rate limiting + Problem Details all wired in their own epics; stub for Foundations.
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Middleware pipeline (per ARCH.md):
//   ExceptionHandler -> AuthN -> TenantContext -> AuthZ -> Idempotency -> RateLimit -> AuditWriter
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Dev/test schema creation. Production runs migrations as a separate deploy job (ARCH.md,
    // Data model section) — never inline at startup.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
}

app.MapDefaultEndpoints();

// Foundations epic: a single ping endpoint to confirm the pipeline works end-to-end.
// Future epics replace this with full endpoint groups (matters, documents, trust, etc.).
app.MapGet("/api/v1/ping", () => Results.Ok(new { service = "TheLawyer.Api", utcNow = DateTimeOffset.UtcNow }))
   .AllowAnonymous();

// Foundations probes (#11): identity resolution, policy gating, tenant-isolation surface.
app.MapFoundationProbes();

app.Run();
