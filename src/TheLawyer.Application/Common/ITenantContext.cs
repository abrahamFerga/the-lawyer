namespace TheLawyer.Application.Common;

/// <summary>
/// Provides the current request's tenant context. Resolved by the Api's
/// tenant-resolution middleware from the JWT `tenant_id` claim and registered
/// in DI as a scoped service.
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant id, or null when running outside a tenant scope (e.g. background jobs that haven't impersonated yet).</summary>
    Guid? TenantId { get; }

    /// <summary>The current user id, or null for anonymous / system contexts.</summary>
    Guid? UserId { get; }

    /// <summary>The current user's role (one of attorney/paralegal/firm-admin/bookkeeper/client), or null.</summary>
    string? Role { get; }

    /// <summary>True when the current scope is operating without tenant filtering (e.g. for cross-tenant admin tasks).</summary>
    bool TenantFilterDisabled { get; }
}
