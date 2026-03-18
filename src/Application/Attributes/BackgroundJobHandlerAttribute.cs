using Domain;

namespace Application.Attributes;

/// <summary>
/// Specifies the <see cref="BackgroundJobType"/> that a class is responsible for handling.
/// </summary>
/// <param name="type">The specific job type to associate with this handler.</param>
/// <remarks>
/// This attribute is used to automatically register and key handlers in the dependency injection container.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class BackgroundJobHandlerAttribute(BackgroundJobType type) : Attribute
{
    public BackgroundJobType Type { get; } = type;
}