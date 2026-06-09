namespace TheLawyer.Domain.Common;

/// <summary>
/// Every domain entity that holds tenant-owned data implements this.
/// The Infrastructure layer applies a global EF Core query filter to enforce isolation:
/// reads never return another tenant's rows; writes set the tenant id automatically.
/// Bypassing the filter requires an explicit IgnoreTenantFilter() call, which is audit-logged.
/// </summary>
public interface ITenantedEntity
{
    Guid TenantId { get; set; }
}
