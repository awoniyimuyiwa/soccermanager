namespace Domain;

/// <summary>
/// Records a player's value for an audit trail of all value movements
/// </summary>
public class PlayerValue : AuditedEntity
{
    public required Guid PlayerId { get; init; }

    public Guid? SourceEntityId { get; init; }

    public PlayerValueType Type { get; init; }

    /// <summary>
    /// + for increase, - for decrease
    /// </summary>
    public required decimal Value { get; init; }
}

public enum PlayerValueType
{
    Initial,
    Transfer
}