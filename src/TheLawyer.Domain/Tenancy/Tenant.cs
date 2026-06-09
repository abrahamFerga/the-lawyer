namespace TheLawyer.Domain.Tenancy;

/// <summary>
/// A law firm. Tenants are the multi-tenancy boundary; every other entity rolls up to one.
/// IoltaConfigJson holds per-firm trust-account configuration (jurisdictions, account references).
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string PrimaryState { get; init; }
    public string IoltaConfigJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
