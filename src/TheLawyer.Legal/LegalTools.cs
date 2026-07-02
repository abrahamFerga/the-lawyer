using System.ComponentModel;
using System.Text;
using Cortex.Modules.Legal.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cortex.Modules.Legal;

/// <summary>
/// The Legal module's clause and playbook tools, backed by the tenant's persisted library (seeded
/// from <see cref="LegalCatalog"/> defaults, curated by the firm from there). Deterministic: the
/// tools search and render — the module's instructions keep the assistant from giving legal advice.
/// </summary>
public sealed class LegalTools(LegalDbContext db)
{
    [Description("Search the firm's clause library by keyword. Returns matching clauses with their category and a short summary.")]
    public async Task<string> SearchClauses(
        [Description("Keywords, e.g. 'confidentiality', 'liability', or 'termination'.")] string query,
        CancellationToken cancellationToken = default)
    {
        var matches = await SearchLibraryAsync(query, cancellationToken);
        if (matches.Count == 0)
        {
            return $"No clauses match \"{query}\". Try a keyword like 'confidentiality', 'liability', or 'termination'.";
        }

        var lines = matches.Take(8).Select(c => $"{c.Title} ({c.Category}) — {c.Summary}");
        return $"Found {matches.Count} clause(s): {string.Join(" | ", lines)}.";
    }

    [Description("Draft a contract clause from the firm's library, filled in with the two party names. Use search_clauses first if unsure of the clause type.")]
    public async Task<string> DraftClause(
        [Description("Clause type or keyword, e.g. 'indemnification' or 'governing law'.")] string clauseType,
        [Description("Name of the first party (e.g. the provider/discloser).")] string partyA,
        [Description("Name of the second party (e.g. the client/recipient).")] string partyB,
        CancellationToken cancellationToken = default)
    {
        var matches = await SearchLibraryAsync(clauseType, cancellationToken);
        var clause = matches.Count > 0 ? matches[0] : null;
        if (clause is null)
        {
            return $"No clause in the firm's library matches \"{clauseType}\". Call search_clauses to find an available clause type first.";
        }

        var body = LegalCatalog.RenderTemplate(clause.Template, partyA, partyB);
        return $"{clause.Title} ({clause.Category}):\n\n{body}\n\nThis is a standard template, not legal advice — have a licensed attorney review before use.";
    }

    [Description("Get the firm's contract-review playbook: the rules to check contracts against, with severity.")]
    public async Task<string> GetPlaybook(CancellationToken cancellationToken = default)
    {
        // Severity persists as a string (readable rows), so order in memory where enum order applies.
        var rules = (await db.PlaybookRules.ToListAsync(cancellationToken))
            .OrderByDescending(r => r.Severity)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rules.Count == 0)
        {
            return "The firm's playbook is empty. An administrator can add rules via the playbook endpoints.";
        }

        var sb = new StringBuilder("Firm playbook (check contracts against every rule):\n");
        foreach (var rule in rules)
        {
            sb.AppendLine($"- [{rule.Severity}] {rule.Title}: {rule.Guidance}");
        }

        return sb.ToString();
    }

    private async Task<IReadOnlyList<TenantClause>> SearchLibraryAsync(string query, CancellationToken cancellationToken)
    {
        // Tenant libraries are small (seed is 8 rows); load once and reuse the shared forgiving search.
        var clauses = await db.Clauses.OrderBy(c => c.Title).Take(500).ToListAsync(cancellationToken);
        return LegalCatalog.Search(clauses, query, c => [c.Title, c.Category, c.Summary, c.Slug]);
    }
}
