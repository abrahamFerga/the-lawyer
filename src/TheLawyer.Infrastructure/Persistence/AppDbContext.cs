using Microsoft.EntityFrameworkCore;
using TheLawyer.Application.Common;
using TheLawyer.Domain.Common;
using TheLawyer.Domain.Tenancy;

namespace TheLawyer.Infrastructure.Persistence;

/// <summary>
/// The single EF Core DbContext for TheLawyer's operational data.
/// Global query filters on every ITenantedEntity enforce multi-tenant isolation;
/// the only way to bypass is via the audit-logged IgnoreTenantFilter() helper.
/// Audit entries live in a separate `audit` schema, intentionally outside this context
/// so that "delete from operational" never implies "delete from audit".
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenant is itself the tenancy root; no tenant filter on it.
        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).HasMaxLength(200);
            b.Property(t => t.PrimaryState).HasMaxLength(2);
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).HasMaxLength(320);
            b.Property(u => u.FullName).HasMaxLength(200);
            b.Property(u => u.Role).HasMaxLength(40);
            b.Property(u => u.IdentityProviderSub).HasMaxLength(200);
            b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        });

        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>
    /// Apply a global query filter to every entity implementing ITenantedEntity:
    /// rows whose TenantId does not match the current request's tenant are invisible
    /// to reads. This is the load-bearing line of multi-tenant isolation.
    /// </summary>
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantedEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            _tenantContext.TenantFilterDisabled || e.TenantId == _tenantContext.TenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantIdOnNewEntities();
        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void StampTenantIdOnNewEntities()
    {
        if (_tenantContext.TenantId is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantedEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = _tenantContext.TenantId.Value;
            }
        }
    }
}
