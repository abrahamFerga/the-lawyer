using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheLawyer.Application.Common;
using TheLawyer.Infrastructure.Persistence;

namespace TheLawyer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wire EF Core (Postgres), the tenant context, and the audit-log abstractions.
    /// </summary>
    public static IServiceCollection AddTheLawyerInfrastructure(this IServiceCollection services, string postgresConnectionString)
    {
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(postgresConnectionString, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        });

        return services;
    }
}
