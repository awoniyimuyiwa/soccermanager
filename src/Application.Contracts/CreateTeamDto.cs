namespace Application.Contracts;

public record CreateTeamDto : CreateUpdateTeamDto
{
    public virtual decimal TransferBudget { get; init; } = Domain.Constants.InitialTeamTransferBudget;  
}
