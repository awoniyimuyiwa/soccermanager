namespace Domain;

public class Team : AuditedEntity, IHasConcurrencyStamp
{
    protected List<Player> Players { get; set; } = [];

    protected List<TransferBudgetValue> TransferBudgetValues { get; set; } = [];

    protected List<Transfer> TransfersFrom { get; set; } = [];

    protected List<Transfer> TransfersTo { get; set; } = [];

    public string? Country { get; set; } 

    public string? Name { get; set; } 

    public long OwnerId { get; protected set; }

    /// <summary>
    /// For tracking current transfer budget, sum of all <see cref="TransferBudgetValues"/>
    /// </summary>
    public decimal TransferBudget { get; set; }

    /// <summary>
    /// For tracking current team vaue, sum of all <see cref="Players"/> values
    /// </summary>
    public decimal Value { get; set; }

    public ApplicationUser Owner { get; protected set; } = null!; 

    // Computed propeties
    public IReadOnlyCollection<Player> AllPlayers => Players;

    public string? ConcurrencyStamp { get; set; }

    public Team() {}

    public Team(
        Guid externalId,
        string? country,
        string? name, 
        ApplicationUser owner)
    {
        ExternalId = externalId;
        Country = country;
        Name = name;
        Owner = owner;
        OwnerId = owner.Id;
    }

    /// <summary>
    /// Recomputes transfer budget from transfer budget values
    /// </summary>
    public void RecomputeTransferBudget()
    {       
        TransferBudget = TransferBudgetValues.Sum(tbv => tbv.Value);
    }

    /// <summary>
    /// Recomputes team value from player values
    /// </summary>
    public void RecomputeValue()
    {
        Value = Players.Sum(p => p.Value);
    }
}
