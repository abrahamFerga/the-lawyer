namespace TheLawyer.Domain.Common;

/// <summary>
/// Tracks who and when an entity was created or last modified.
/// Captured automatically by the audit interceptor; do not set manually.
/// </summary>
public interface IAuditedEntity
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? ModifiedAt { get; }
    Guid CreatedByUserId { get; }
    Guid? ModifiedByUserId { get; }
}
