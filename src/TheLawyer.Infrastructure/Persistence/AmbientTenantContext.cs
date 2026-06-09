using TheLawyer.Application.Common;

namespace TheLawyer.Infrastructure.Persistence;

/// <summary>
/// In-process implementation of <see cref="ITenantContext"/>. The Api layer's
/// tenant-resolution middleware populates this scoped instance from the request's
/// JWT claims at the start of every request; background jobs that need to impersonate
/// a tenant set it explicitly via SetForJob().
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public bool TenantFilterDisabled { get; private set; }

    public void SetForRequest(Guid? tenantId, Guid? userId, string? role)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        TenantFilterDisabled = false;
    }

    public void SetForJob(Guid tenantId)
    {
        TenantId = tenantId;
        TenantFilterDisabled = false;
    }

    /// <summary>
    /// DANGEROUS — disables the tenant filter for the rest of this scope.
    /// Audit-logged on every use; intended for cross-tenant administrative tasks only.
    /// </summary>
    public void IgnoreTenantFilter()
    {
        TenantFilterDisabled = true;
    }
}
