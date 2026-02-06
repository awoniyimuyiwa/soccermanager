namespace Domain;

public static class EntityExtensions
{
    public static TeamDto ToDto(this Team team)
    {
       return new TeamDto(
           team.ExternalId,
           team.Country,
           team.Name,
           team.Owner.FirstName,
           team.Owner.ExternalId,
           team.Owner.LastName,
           team.TransferBudget,
           team.Value,
           team.CreatedAt,
           team.UpdatedAt,
           team.ConcurrencyStamp);
    }

    public static PlayerDto ToDto(
        this Player player, 
        DateOnly today)
    {
        return new PlayerDto(
            player.ExternalId,
            player.GetAge(today),
            player.Country,
            player.DateOfBirth,
            player.FirstName,
            player.LastName,
            player.Team.ExternalId,
            player.Team.Name,
            player.Type,
            player.Value,
            player.CreatedAt,
            player.UpdatedAt,
            player.ConcurrencyStamp);
    }

    public static TransferDto ToDto(
        this Transfer transfer)
    {
        return new TransferDto(
            transfer.ExternalId,
            transfer.AskingPrice,
            transfer.FromTeam.ExternalId,
            transfer.Player.ExternalId,
            transfer.ToTeam?.ExternalId,
            transfer.CreatedAt,
            transfer.UpdatedAt,
            transfer.ConcurrencyStamp);
    }
}
