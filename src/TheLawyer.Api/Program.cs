using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using TheLawyer.Api.Middleware;
using TheLawyer.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults: OpenTelemetry, health checks, resilient HTTP, service discovery.
builder.AddServiceDefaults();

// Infrastructure: EF Core, AmbientTenantContext, audit log abstractions.
var postgresConn = builder.Configuration.GetConnectionString("postgres")
    ?? throw new InvalidOperationException("Connection string 'postgres' missing. Aspire AppHost should supply it.");
builder.Services.AddTheLawyerInfrastructure(postgresConn);

// AuthN: JWT Bearer against Entra ID B2C.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.Bind("AzureAdB2C", options);
        // The tenant_id and role custom claims are extracted by TenantResolutionMiddleware.
    });

// AuthZ: policies named <Module>.<Action>. Bound to roles via appsettings.json
// (e.g. "Rbac:RoleAssignments:firm-admin:[Matters.Open, Trust.Post, ...]").
// Foundations epic ships one example policy; the rest land with their respective epics.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Matters.View", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("role", "attorney", "paralegal", "firm-admin", "client"));
});

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
}

app.MapDefaultEndpoints();

// Foundations epic: a single ping endpoint to confirm the pipeline works end-to-end.
// Future epics replace this with full endpoint groups (matters, documents, trust, etc.).
app.MapGet("/api/v1/ping", () => Results.Ok(new { service = "TheLawyer.Api", utcNow = DateTimeOffset.UtcNow }))
   .AllowAnonymous();

app.Run();
