using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Migration to create 'sp_MaintainRunningBalanceIndexes' that reorganizes or rebuilds fragmented indexes for optimal performance
    /// </summary>
    /// <remarks>
    /// <para>SQL SERVER AGENT JOB CONFIGURATION:</para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// Create New Job: Name it 'SoccerManager_RunningBalance_Index_Maintenance'.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Step Setup: Type = T-SQL, Command = 'EXEC [dbo].[sp_MaintainRunningBalanceIndexes];'. 
    /// **IMPORTANT**: Set 'Database' dropdown to the application database, not 'master'.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Schedule: Weekly, Recurs every 1 week on Sunday at 03:00 AM.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Notifications: Requires Database Mail. Set 'Operator' to receive email on job completion/failure.
    /// </description>
    /// </item>
    /// </list>
    /// <para>QUICK VERIFICATION TIP:</para>
    /// <list type="bullet">
    /// <item>
    /// <description>Manual Test: Right-click the job in SSMS and select 'Start Job at Step...'. 
    /// This confirms the script runs and the email notification fires without waiting for the Sunday schedule.</description>
    /// </item>
    /// </list>
    /// <para>CRITICAL NOTES:</para>
    /// <list type="bullet">
    /// <item>
    /// <description>Permissions: The SQL Agent Service Account requires ALTER permissions on 'Teams', 'Players', 'PlayerValues', and 'TeamTransferBudgetValues'.</description>
    /// </item>
    /// <item>
    /// <description>The 'Online' Flag: Logic currently excludes 'ONLINE = ON'. Re-enable only if using Enterprise/Azure SQL and tables lack LOB columns.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public partial class AddStoredProcedureForMaintainingRunningBalanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE PROCEDURE sp_MaintainRunningBalanceIndexes
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @cmd NVARCHAR(MAX);
                    -- Select indexes for our specific tables with > 10% fragmentation
                    DECLARE MaintenanceCursor CURSOR FOR
                    SELECT 'ALTER INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(t.name) +
                           (CASE WHEN s.avg_fragmentation_in_percent > 30 THEN ' REBUILD' ELSE ' REORGANIZE' END) +
                           ' WITH (ONLINE = ON)' -- Use ONLINE if on Enterprise/Azure, else remove this part
                    FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') s
                    JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
                    JOIN sys.tables t ON i.object_id = t.object_id
                    WHERE t.name IN ('Teams', 'Players', 'PlayerValues', 'TransferBudgetValues')
                      AND s.avg_fragmentation_in_percent > 10;
                    OPEN MaintenanceCursor;
                    FETCH NEXT FROM MaintenanceCursor INTO @cmd;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC sp_executesql @cmd;
                        FETCH NEXT FROM MaintenanceCursor INTO @cmd;
                    END
                    CLOSE MaintenanceCursor;
                    DEALLOCATE MaintenanceCursor;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_MaintainRunningBalanceIndexes");
        }
    }
}
