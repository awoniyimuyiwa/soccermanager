namespace Domain;

public class Constants
{
    public const string AdminRoleName = "Admin";

    public const decimal InitialPlayerValue = 1_000_000;
    public const decimal InitialTeamTransferBudget = 5_000_000;
    public const string InitialValueDescription = "Initial";

    public const int MaxPlayerValuePercentageIncrease = 100;

    public const int MinPageSize = 1;
    public const int MinPageNumber = 1;
    public const int MinPlayerAge = 18;
    public const decimal MinPlayerAskingPrice = 0;
    public const decimal MinPlayerValue = 0;
    public const int MinPlayerValuePercentageIncrease = 10;
    public const decimal MinTeamTransferBudget = 0;

    public const string MustOwnATeamForTransferErrorMessage = "Only users who own a team can pay for transfer.";

    public const int MaxPageSize = 100;
    public const int MaxPlayerAge = 40;
    
    public const string PlayerAlreadyOnTransferListErrorMessage = "Player is already on transfer list.";

    /// <summary>
    /// Min length for string fields
    /// </summary>
    public const int StringMinLength = 3;

    /// <summary>
    /// Max length for string fields
    /// </summary>
    public const int StringMaxLength = 255;

    public const string TransferAlreadyCompletedErrorMessage = "Transfer already completed.";
    public const string TransferBudgetIsInsufficientErrorMessage = "Transfer budget of destination team is insufficient.";
    public const string TransferCantBeToTheSameTeamErrorMessage = "Transfer can't be to the same team.";
    public const string TransferDescription = "Transfer";   
}
