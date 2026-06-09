using System.Security.Claims;
using TheLawyer.Infrastructure.Persistence;

namespace TheLawyer.Api.Middleware;

/// <summary>
/// Populates the request-scoped <see cref="AmbientTenantContext"/> from the JWT claims
/// added by Entra ID B2C: <c>tenant_id</c> and <c>role</c>. Without this middleware,
/// every EF query would return zero rows (the global tenant filter would have nothing to match).
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, AmbientTenantContext tenantContext)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = user.FindFirstValue("tenant_id");
            var userClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            var role = user.FindFirstValue("role");

            Guid? tenantId = Guid.TryParse(tenantClaim, out var t) ? t : null;
            Guid? userId = Guid.TryParse(userClaim, out var u) ? u : null;
            tenantContext.SetForRequest(tenantId, userId, role);
        }

        await _next(context).ConfigureAwait(false);
    }
}
