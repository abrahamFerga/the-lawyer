using Microsoft.AspNetCore.Authorization;

namespace TheLawyer.Api.Auth;

/// <summary>
/// Binds the RBAC scaffold from configuration (per ARCH.md cross-cutting table): policies are
/// named <c>&lt;Module&gt;.&lt;Action&gt;</c> and <c>Rbac:RoleAssignments</c> maps each ROLE to the
/// policies it holds. Code references policy names, never role names (SPEC RBAC model) — retuning
/// who may do what is a configuration change, not a code change.
/// </summary>
public static class RbacAuthorization
{
    public const string SectionName = "Rbac";

    public static IServiceCollection AddTheLawyerAuthorization(
        this IServiceCollection services, IConfiguration configuration)
    {
        var assignments = configuration
            .GetSection($"{SectionName}:RoleAssignments")
            .Get<Dictionary<string, string[]>>() ?? new Dictionary<string, string[]>();

        // Invert role -> policies into policy -> roles, so each policy requires the role claim
        // of every role that holds it.
        var rolesByPolicy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (role, policies) in assignments)
        {
            foreach (var policy in policies)
            {
                if (!rolesByPolicy.TryGetValue(policy, out var roles))
                {
                    rolesByPolicy[policy] = roles = [];
                }

                roles.Add(role);
            }
        }

        services.AddAuthorization(options =>
        {
            foreach (var (policy, roles) in rolesByPolicy)
            {
                options.AddPolicy(policy, p => p
                    .RequireAuthenticatedUser()
                    .RequireClaim("role", roles.ToArray()));
            }
        });

        return services;
    }
}
