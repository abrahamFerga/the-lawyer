using TheLawyer.Domain.Common;

namespace TheLawyer.Domain.Tenancy;

/// <summary>
/// A user belonging to a tenant. The role drives RBAC; policies are bound to roles
/// via appsettings.json so industry-specific firms can customise without recompiling.
/// </summary>
public sealed class User : ITenantedEntity
{
    public Guid Id { get; init; }
    public Guid TenantId { get; set; }
    [Pii] public required string Email { get; set; }
    [Pii] public required string FullName { get; set; }
    public required string Role { get; set; }
    public required string IdentityProviderSub { get; init; }
}
