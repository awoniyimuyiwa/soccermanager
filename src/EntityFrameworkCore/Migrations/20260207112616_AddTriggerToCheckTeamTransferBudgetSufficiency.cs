using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Trigger to check team transfer budget sufficiency combined with existing trigger to calculate transfer budget balance.
    /// </summary>
    /// <remarks>
    /// <para>RAISERROR LOGIC (16, 1):</para>
    /// <list type="bullet">
    /// <item>
    /// <term>Severity 16</term>
    /// <description>The standard level for business logic violations. Microsoft.Data.SqlClient only treats a message as an Exception if the severity is 11 or higher.</description>
    /// </item>
    /// <item>
    /// <term>State 1</term>
    /// <description>An arbitrary integer (0-255) used to identify the specific location of the error within the T-SQL code.</description>
    /// </item>
    /// </list>
    /// <para>DEBUGGING TIP:</para>
    /// <description>
    /// If you have multiple IF statements in one trigger returning the same error message, 
    /// assign each a unique State (e.g., State 1, State 2) to pinpoint which check failed.
    /// </description>
    /// 
    /// <para>DATA INTEGRITY NOTE:</para>
    /// <description>
    /// EXECUTION ORDER: Check Constraint -> Trigger -> Optimistic concurrency check
    /// If the constraint fails, the Trigger's custom RAISERROR will not be reached. 
    /// To test the trigger, temporarily drop check check constraint.
    /// </description>
    /// </remarks>
    public partial class AddTriggerToCheckTeamTransferBudgetSufficiency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                ALTER TRIGGER {Constants.TeamTransferBudgetTriggerName} ON TransferBudgetValues
                AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    -- 1. Perform the calculation (Update the Team balance)
                    UPDATE Teams SET TransferBudget = TransferBudget + ISNULL(Changes.NetChange, 0)
                    FROM Teams 
                    INNER JOIN
                    (
                        SELECT TeamId, SUM(Diff) AS NetChange FROM 
                        (
                            SELECT TeamId, Value AS Diff FROM inserted
                            UNION ALL
                            SELECT TeamId, -Value AS Diff FROM deleted
                        ) AS Combined GROUP BY TeamId
                    ) AS Changes ON Teams.Id = Changes.TeamId;

                    -- 2. Added Balance Check: Prevent negative TransferBudget
                    IF EXISTS (SELECT 1 FROM Teams WHERE TransferBudget < 0)
                    BEGIN
                        -- This will stop the transaction and notify the sql client.
                        RAISERROR('{Domain.Constants.InsufficientTeamTransferBudgetErrorMessage}', 16, 1);
                        ROLLBACK TRANSACTION;
                        RETURN;
                    END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the trigger back to the version WITHOUT the balance check
            migrationBuilder.Sql($"""
                ALTER TRIGGER {Constants.TeamTransferBudgetTriggerName} ON TransferBudgetValues
                AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    UPDATE Teams SET TransferBudget = TransferBudget + ISNULL(Changes.NetChange, 0)
                    FROM Teams 
                    INNER JOIN
                    (
                        SELECT TeamId, SUM(Diff) AS NetChange FROM 
                        (
                            SELECT TeamId, Value AS Diff FROM inserted
                            UNION ALL
                            SELECT TeamId, -Value AS Diff FROM deleted
                        ) AS Combined GROUP BY TeamId
                    ) AS Changes ON Teams.Id = Changes.TeamId;
                END
                """);
        }
    }
}
