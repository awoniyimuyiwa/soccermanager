using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerAndTeamTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Initialize existing data first to avoid trigger double counting
            migrationBuilder.Sql("""
                WITH PlayerValueTotals AS 
                (
                  SELECT PlayerId, SUM(Value) as Total FROM PlayerValues GROUP BY PlayerId
                )
                UPDATE p SET p.Value = ISNULL(pvt.Total, 0)
                FROM Players p LEFT JOIN PlayerValueTotals pvt ON p.Id = pvt.PlayerId;
                """);

            migrationBuilder.Sql("""
                WITH TeamValueTotals AS 
                (
                  SELECT TeamId, SUM(Value) as Total FROM Players GROUP BY TeamId
                )
                UPDATE t SET t.Value = ISNULL(tvt.Total, 0)
                FROM Teams t LEFT JOIN TeamValueTotals tvt ON t.Id = tvt.TeamId;
                """);

            migrationBuilder.Sql("""
                WITH TeamTransferBudgetTotals AS 
                (
                  SELECT TeamId, SUM(Value) as Total FROM TransferBudgetValues GROUP BY TeamId
                )
                UPDATE t SET t.TransferBudget = ISNULL(tbt.Total, 0)
                FROM Teams t LEFT JOIN TeamTransferBudgetTotals tbt ON t.Id = tbt.TeamId;
                """);

            // 2. Enable Recursive Triggers so Player updates trigger Team updates
            //  suppressTransaction to prevent EF Core: ALTER DATABASE statement not allowed within multi-statement transaction.
            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET RECURSIVE_TRIGGERS ON;",
                 suppressTransaction: true);

            // 3. Trigger: PlayerValues -> Players
            // SET NOCOUNT ON: Prevents the number of rows affected from being reported to EF core and breaking optimistic concurreny checks
            migrationBuilder.Sql($"""
                CREATE TRIGGER {Constants.PlayerValueTriggerName} ON PlayerValues
                AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                SET NOCOUNT ON;
                UPDATE Players SET Value = Value + ISNULL(Changes.NetChange, 0)
                FROM Players 
                INNER JOIN
                (
                  SELECT PlayerId, SUM(Diff) AS NetChange FROM 
                  (
                    SELECT PlayerId, Value AS Diff FROM inserted
                    UNION ALL
                    SELECT PlayerId, -Value AS Diff FROM deleted
                  ) AS Combined GROUP BY PlayerId
                ) AS Changes ON Players.Id = Changes.PlayerId;
                END
                """);

            // 4. Trigger: Players -> Teams
            // This handles:
            // 1.Value changes(caused by the PlayerValues trigger)
            // 2.Players changing Teams(TeamId swap)
            // 3.Adding / Removing players
            migrationBuilder.Sql($"""
                CREATE TRIGGER {Constants.TeamValueTriggerName} ON Players
                AFTER INSERT, UPDATE, DELETE AS
                BEGIN
                SET NOCOUNT ON;
                UPDATE Teams SET Value = Value + ISNULL(Changes.NetChange, 0)
                FROM Teams 
                INNER JOIN
                (
                  SELECT TeamId, SUM(Diff) AS NetChange FROM 
                  (
                    SELECT TeamId, Value AS Diff FROM inserted
                    UNION ALL
                    SELECT TeamId, -Value AS Diff FROM deleted
                  ) AS Combined 
                  GROUP BY TeamId
                ) AS Changes ON Teams.Id = Changes.TeamId;
                END
                """);

            // 5. Trigger: TransferBudgetValues -> Teams
            migrationBuilder.Sql($"""
                CREATE TRIGGER {Constants.TeamTransferBudgetTriggerName} ON TransferBudgetValues
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DROP TRIGGER {Constants.TeamTransferBudgetTriggerName}");
            migrationBuilder.Sql($"DROP TRIGGER {Constants.TeamValueTriggerName}");
            migrationBuilder.Sql($"DROP TRIGGER {Constants.PlayerValueTriggerName}");
            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET RECURSIVE_TRIGGERS OFF;",
                 suppressTransaction: true);

        }
    }
}
