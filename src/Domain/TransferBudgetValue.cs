namespace Domain;

/// <summary>
/// Records a team's transfer budget value for an audit trail of all budget value movements
/// </summary>
public class TransferBudgetValue : AuditedEntity
{
    public string? Description { get; init; }

    public Guid? TransferId { get; protected set; }

    public Guid TeamId { get; set; }

    /// <summary>
    /// + for increase, - for decrease
    /// </summary>
    public decimal Value { get; init; }

    /// <summary>
    /// Set when the value is for a player transfer
    /// </summary>
    public Transfer? Transfer { get; init; }

    public TransferBudgetValue() {}

    public TransferBudgetValue(
        Guid id,
        Guid teamId,
        decimal value,
        string? description = null,
        Transfer? transfer = null)
    {
        Id = id;
        Description = description;
        Transfer = transfer;
        TransferId = transfer?.Id;
        TeamId = teamId;
        Value = value;       
    }
}
