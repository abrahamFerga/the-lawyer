namespace TheLawyer.Domain.Common;

/// <summary>
/// Marks a property as containing Personally Identifiable Information.
/// Flows through audit logging (values redacted in before/after JSON),
/// data-subject-export handlers, and PII-aware telemetry sampling.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PiiAttribute : Attribute;
