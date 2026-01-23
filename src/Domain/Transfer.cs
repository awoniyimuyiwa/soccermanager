namespace Domain;

/// <summary>
/// Records pending and completed player transfers for an audit trail.
/// Team owners will only see the ones where FromTeam or ToTeam belongs to them
/// </summary>
public class Transfer : AuditedEntity, IHasConcurrencyStamp
{
    private Guid? _toTeamId;
    
    private Team? _toTeam;

    public decimal AskingPrice { get; set; }

    public Guid FromTeamId { get; protected set; }

    public Guid PlayerId { get; protected set; }

    /// <summary>
    /// Set when transfer completes
    /// </summary>
    public Guid? ToTeamId
    {
        get => _toTeamId;
        protected set => _toTeamId = value;
    }

    public Team FromTeam { get; protected set; } = null!;

    public Player Player { get; protected set; } = null!;

    /// <summary>
    /// Set when transfer completes
    /// </summary>
    public Team? ToTeam
    {
        get => _toTeam;
        set
        {
            _toTeam = value;
            _toTeamId = value?.Id;
        }
    }

    /// <summary>
    /// For optimistic concurrency
    /// </summary>
    public string? ConcurrencyStamp { get; set; }

    public Transfer() {}

    public Transfer(
        Guid id,
        decimal askingPrice,
        Team fromTeam,
        Player player)
    {
        Id = id;
        AskingPrice = askingPrice;
        FromTeam = fromTeam;
        FromTeamId = fromTeam.Id;
        Player = player;
        PlayerId = player.Id;
    }
}
