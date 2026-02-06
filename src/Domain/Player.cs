namespace Domain;

public class Player : AuditedEntity, IHasConcurrencyStamp
{
    private long _teamId;

    private Team _team = null!;

    protected List<PlayerValue> PlayerValues { get; set; } = [];

    protected List<Transfer> Transfers { get; set; } = [];

    public string? Country { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public PlayerType Type { get; set; }

    /// <summary>
    /// For tracking current value, sum of all <see cref="PlayerValues"/>
    /// </summary>
    public decimal Value { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public long TeamId
    {
        get => _teamId;
        protected set => _teamId = value;
    }

    public Team Team
    {
        get => _team;
        set
        {
            _team = value;
            _teamId = value.Id;
        }
    }

    public Player() {}

    public Player(
        Guid externalId,
        string? country,
        DateOnly dateOfBirth,
        string? firstName,
        string? lastName,
        Team team,
        PlayerType type)
    {
        ExternalId = externalId;
        Country = country;
        DateOfBirth = dateOfBirth;
        FirstName = firstName;
        LastName = lastName;
        Team = team;
        TeamId = team.Id;
        Type = type;
    }

    public int GetAge(DateOnly today)
    {
        var age = today.Year - DateOfBirth.Year;
        if (today < DateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }
}

public enum PlayerType
{
    Goalkeeper,
    Defender,
    Midfielder,
    Attacker
}
