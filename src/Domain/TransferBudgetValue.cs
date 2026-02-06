namespace Domain;

/// <summary>
/// Records a team's transfer budget value for an audit trail of all budget value movements
/// </summary>
public class TransferBudgetValue : AuditedEntity
{
    public string? Description { get; init; }

    public long? TransferId { get; protected set; }

    public long TeamId { get; protected set; }

    /// <summary>
    /// + for increase, - for decrease
    /// </summary>
    public decimal Value { get; init; }

    public Team Team { get; protected set; } = null!;

    /// <summary>
    /// Set when the value is for a player transfer
    /// </summary>
    public Transfer? Transfer { get; init; }

    public TransferBudgetValue() {}

    public TransferBudgetValue(
        Guid externalId,
        Team team,
        decimal value,
        string? description = null,
        Transfer? transfer = null)
    {
        ExternalId = externalId;
        Description = description;
        Team = team;
        TeamId = team.Id;
        Transfer = transfer;
        TransferId = transfer?.Id;
        Value = value;       
    }
}
