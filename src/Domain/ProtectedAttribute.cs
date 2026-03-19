namespace Domain;

/// <summary>
/// Specifies that a string property (e.g., a cursor) should be automatically encrypted
/// during JSON serialization before being sent to the client.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ProtectedAttribute() : Attribute {}
