namespace Domain;

/// <summary>
/// Specifies that a string property (e.g., an API Key) should be automatically masked 
/// during JSON serialization before being sent to the client.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MaskedAttribute : Attribute { }
