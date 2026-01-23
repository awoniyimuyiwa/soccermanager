namespace Domain;

public class Team : AuditedEntity, IHasConcurrencyStamp
{
    protected List<Player> Players { get; set; } = [];

    protected List<TransferBudgetValue> TransferBudgetValues { get; set; } = [];

    protected List<Transfer> TransfersFrom { get; set; } = [];

    protected List<Transfer> TransfersTo { get; set; } = [];

    public string? Country { get; set; } 

    public string? Name { get; set; } 

    public Guid OwnerId { get; protected set; }

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
        Guid id,
        string? country,
        string? name, 
        ApplicationUser owner,
        DateTime currentDate)
    {   
        Id = id;
        Country = country;
        Name = name;
        Owner = owner;
        OwnerId = owner.Id;

        TransferBudgetValues.Add(new TransferBudgetValue(
            id,
            Id,
            Constants.InitialTeamTransferBudget,
            Constants.InitialValueDescription,
            null));
        TransferBudget += Constants.InitialTeamTransferBudget;
       
        var random = new Random();

        var players = new List<Player>();
        players.AddRange(Enumerable.Range(1, 3).Select(index => new Player(
            id: Guid.NewGuid(),
            country: null,
            dateOfBirth: GetRandomDateOfBirth(currentDate, random),
            firstName: null,
            lastName: null,
            team: this,
            type: PlayerType.Goalkeeper)));

        players.AddRange(Enumerable.Range(1, 6).Select(index => new Player(
            id: Guid.NewGuid(),
            country: null,
            dateOfBirth: GetRandomDateOfBirth(currentDate, random),
            firstName: null,
            lastName: null,
            team: this,
            type: PlayerType.Defender)));

        players.AddRange(Enumerable.Range(1, 6).Select(index => new Player(
            id: Guid.NewGuid(),
            country: null,
            dateOfBirth: GetRandomDateOfBirth(currentDate, random),
            firstName: null,
            lastName: null,
            team: this,
            type: PlayerType.Midfielder)));

        players.AddRange(Enumerable.Range(1, 5).Select(index => new Player(
            id: Guid.NewGuid(),
            country: null,
            dateOfBirth: GetRandomDateOfBirth(currentDate, random),
            firstName: null,
            lastName: null,
            team: this,
            type: PlayerType.Attacker)));

        AddPlayers(players);
    }

    /// <summary>
    /// Add players, update team value
    /// </summary>
    /// <param name="players"></param>
    /// <exception cref="DomainException">When player team id is that of a different team</exception>
    public void AddPlayers(IEnumerable<Player> players)
    {
        foreach (Player player in players)
        {
            if (player.TeamId != Id)
            {
                throw new DomainException($"{nameof(player.TeamId)} != {Id}");
            }
                
            Players.Add(player);
        }
        RecomputeValue();
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

    private DateOnly GetRandomDateOfBirth(
        DateTime today,
        Random random)
    {
        var latestPossibleDOB = today.AddYears(-Constants.MinPlayerAge);

        // Exact date for someone who turns Max + 1 today
        var boundaryDate = today.AddYears(-Constants.MaxPlayerAge - 1);
        // One day after their Max + 1 birthday is the earliest they can be born to be Max
        var earliestPossibleDOB = boundaryDate.AddDays(1);
       
        // Calculate total days span
        int rangeInDays = (latestPossibleDOB - earliestPossibleDOB).Days;

        // Generate random DOB
        var randomDOB = earliestPossibleDOB.AddDays(random.Next(rangeInDays + 1));

        return DateOnly.FromDateTime(randomDOB);
    }
}
