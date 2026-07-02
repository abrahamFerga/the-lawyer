using Microsoft.EntityFrameworkCore;
using TheLawyer.Application.Common;
using TheLawyer.Domain.Tenancy;
using TheLawyer.Infrastructure.Persistence;

namespace TheLawyer.Api.Endpoints;

/// <summary>
/// Foundations probes (#11): the minimal authenticated surface that makes the epic's acceptance
/// criteria mechanically verifiable before the real module endpoints exist — identity resolution,
/// policy gating (incl. separation of duties and the bookkeeper privilege boundary), and the
/// tenant query filter over a real table. Later epics supersede these with their own endpoints;
/// the probes stay as the cross-cutting regression surface.
/// </summary>
public static class FoundationProbes
{
    public static void MapFoundationProbes(this IEndpointRouteBuilder app)
    {
        // Everything under /api/v1/probes requires an authenticated caller — an anonymous
        // request is 401 before any handler runs.
        var probes = app.MapGroup("/api/v1/probes").RequireAuthorization();

        // Criterion: a valid token resolves tenant + user + role.
        probes.MapGet("/whoami", (ITenantContext tenant) =>
            Results.Ok(new { tenantId = tenant.TenantId, userId = tenant.UserId, role = tenant.Role }));

        // Policy gates straight off Rbac:RoleAssignments — no role names in code.
        probes.MapGet("/matters-view", () => Results.Ok(new { policy = "Matters.View" }))
            .RequireAuthorization("Matters.View");

        // Separation of duties: attorneys draft invoices but cannot approve them.
        probes.MapGet("/invoices-approve", () => Results.Ok(new { policy = "Invoices.Approve" }))
            .RequireAuthorization("Invoices.Approve");

        // Bookkeeper privilege boundary: may post to trust, may NOT see matter content.
        probes.MapGet("/trust-post", () => Results.Ok(new { policy = "Trust.Post" }))
            .RequireAuthorization("Trust.Post");

        // Tenant-isolation probe over a real ITenantedEntity: rows are stamped with the caller's
        // tenant on save and invisible to every other tenant via the global query filter.
        probes.MapPost("/users", async (ProbeUserRequest body, AppDbContext db, ITenantContext tenant) =>
            {
                if (tenant.TenantId is null)
                {
                    return Results.BadRequest(new { error = "Token carries no tenant_id claim." });
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.TenantId.Value,
                    Email = body.Email,
                    FullName = body.FullName,
                    Role = "attorney",
                    IdentityProviderSub = Guid.NewGuid().ToString("N"),
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
                return Results.Created($"/api/v1/probes/users/{user.Id}", new { user.Id });
            })
            .RequireAuthorization("Users.Manage");

        probes.MapGet("/users", async (AppDbContext db) =>
            Results.Ok(await db.Users
                .OrderBy(u => u.Email)
                .Select(u => new { u.Id, u.Email })
                .ToListAsync()))
            .RequireAuthorization("Users.Manage");
    }

    public sealed record ProbeUserRequest(string Email, string FullName);
}
