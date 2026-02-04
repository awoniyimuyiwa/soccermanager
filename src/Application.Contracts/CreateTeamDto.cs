namespace Application.Contracts;

public record CreateTeamDto : CreateUpdateTeamDto
{
    public virtual decimal TransferBudget { get; set; } = Domain.Constants.InitialTeamTransferBudget;  
}
