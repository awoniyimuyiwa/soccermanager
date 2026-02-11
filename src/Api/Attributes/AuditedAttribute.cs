namespace Api.Attributes;

/// <summary>
/// Mark a controller or action to be audited by <see cref="MiddleWares.AuditLogMiddleware"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuditedAttribute : Attribute { }

