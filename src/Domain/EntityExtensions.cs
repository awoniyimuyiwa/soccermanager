namespace Domain;

public static class EntityExtensions
{
    public static TeamDto ToDto(this Team team)
    {
       return new TeamDto(
           team.Id,
           team.Country,
           team.Name,
           team.Owner.FirstName,
           team.OwnerId,
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
            player.Id,
            player.GetAge(today),
            player.Country,
            player.DateOfBirth,
            player.FirstName,
            player.LastName,
            player.TeamId,
            player.Value,
            player.CreatedAt,
            player.UpdatedAt,
            player.ConcurrencyStamp);
    }

    public static TransferDto ToDto(
        this Transfer transfer)
    {
        return new TransferDto(
            transfer.Id,
            transfer.AskingPrice,
            transfer.FromTeamId,
            transfer.PlayerId,
            transfer.ToTeamId,
            transfer.CreatedAt,
            transfer.UpdatedAt,
            transfer.ConcurrencyStamp);
    }
}
