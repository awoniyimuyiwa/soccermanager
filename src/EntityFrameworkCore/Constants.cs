namespace EntityFrameworkCore
{
    /// <summary>
    /// Constants used across the EntityFrameworkCore project, such as error messages and trigger names.
    /// </summary>
    /// <remarks>
    /// If error message constant contains a single quote (e.g., "Team's budget"), 
    /// remember that SQL requires two single quotes to escape it ('') when used in  a RAISERROR or migrationBuilder.Sql command.
    /// </remarks>
    internal class Constants
    {
        public const string PlayerValueTriggerName = "trg_UpdatePlayerValueFromValues";
        
        public const string TeamTransferBudgetCheckConstraintName = "CK_Team_Transfer_Budget";
        public const string TeamTransferBudgetTriggerName = "trg_UpdateTeamTransferBudgetFromValues";
        public const string TeamValueTriggerName = "trg_UpdateTeamValueFromPlayers"; 
    }
}
