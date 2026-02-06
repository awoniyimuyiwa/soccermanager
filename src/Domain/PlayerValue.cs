namespace Domain;

/// <summary>
/// Records a player's value for an audit trail of all value movements
/// </summary>
public class PlayerValue : AuditedEntity
{
    public long PlayerId { get; protected set; }

    public long? SourceEntityId { get; init; }

    public PlayerValueType Type { get; init; }

    /// <summary>
    /// + for increase, - for decrease
    /// </summary>
    public decimal Value { get; init; } = 0;

    public Player Player { get; protected set; } = null!;

    public PlayerValue() { }

    public PlayerValue(
        Guid externalId,
        Player player,
        PlayerValueType type,
        decimal value,
        long? sourceEntityId = null)
    {
        ExternalId = externalId;
        Player = player;
        PlayerId = player.Id;
        Type = type;
        Value = value;
        SourceEntityId = sourceEntityId;
    }
}


public enum PlayerValueType
{
    Initial,
    Transfer
}