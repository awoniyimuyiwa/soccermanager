using Api.Options;

namespace Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class IdempotentAttribute : Attribute
{
    /// <summary>
    /// The number of minutes to cache the response. 
    /// Uses the default from <see cref="IdempotencyOptions"/> if the value is 0.
    /// </summary>
    public uint RecordTTLMinutes { get; set; }
}



