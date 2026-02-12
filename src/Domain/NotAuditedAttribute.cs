namespace Domain;

/// <summary>
/// should be added to properties that shouldn't be audited, so they won't be included in the audit logs when changed. This is useful for properties that are either not important to track or could contain sensitive information that shouldn't be logged.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NotAuditedAttribute : Attribute { }
