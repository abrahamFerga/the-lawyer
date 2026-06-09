namespace TheLawyer.Application.Common;

/// <summary>
/// Append-only audit log abstraction. The infrastructure implementation writes to a
/// separate Postgres schema (`audit.audit_entries`) and redacts PII fields automatically
/// via the [Pii] attribute on domain entities.
/// </summary>
public interface IAuditLog
{
    /// <summary>Record a single audit entry. Idempotency-keyed; safe to call from retried operations.</summary>
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single audit log entry. Holds before/after snapshots as JSON; PII fields are redacted
/// before serialisation. The IdempotencyKey allows safe retries — a duplicate key is a no-op.
/// </summary>
public sealed record AuditEntry(
    Guid TenantId,
    Guid ActorUserId,
    string ActorRole,
    string Action,
    string ResourceType,
    Guid ResourceId,
    string? BeforeJson,
    string? AfterJson,
    string? IdempotencyKey);
