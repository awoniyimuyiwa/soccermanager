using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdToTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // STEP 1: Add new BIGINT columns without making them PKs yet
            migrationBuilder.AddColumn<long>("InternalId", "AspNetUsers", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "AspNetRoles", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "AuditLog", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "Players", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "PlayerValues", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "Teams", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "Transfers", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");
            migrationBuilder.AddColumn<long>("InternalId", "TransferBudgetValues", "bigint", nullable: false, defaultValue: 0L).Annotation("SqlServer:Identity", "1, 1");

            // Add new BIGINT columns for foreign keys without making them FKs yet.
            migrationBuilder.AddColumn<long>("RoleInternalId", "AspNetRoleClaims", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("UserInternalId", "AspNetUserClaims", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("UserInternalId", "AspNetUserLogins", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("UserInternalId", "AspNetUserRoles", "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("RoleInternalId", "AspNetUserRoles", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("UserInternalId", "AspNetUserTokens", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("UserInternalId", "AuditLog", "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("TeamInternalId", "Players", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("PlayerInternalId", "PlayerValues", "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("SourceEntityInternalId", "PlayerValues", "bigint", nullable: true);

            migrationBuilder.AddColumn<long>("OwnerInternalId", "Teams", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("FromTeamInternalId", "Transfers", "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("ToTeamInternalId", "Transfers", "bigint", nullable: true);
            migrationBuilder.AddColumn<long>("PlayerInternalId", "Transfers", "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.AddColumn<long>("TeamInternalId", "TransferBudgetValues", "bigint", nullable: false, defaultValue: 0L);
            migrationBuilder.AddColumn<long>("TransferInternalId", "TransferBudgetValues", "bigint", nullable: true);

            // STEP 2: Link Entities using the new IDs on tables
            // This connects the dots before we break the GUID link, // Id is the old GUID

            migrationBuilder.Sql("""
                UPDATE rc        
                SET rc.RoleInternalId = r.InternalId
                FROM AspNetRoleClaims rc
                JOIN AspNetRoles r ON rc.RoleId = r.Id
                """);

            migrationBuilder.Sql("""
                UPDATE uc        
                SET uc.UserInternalId = u.InternalId
                FROM AspNetUserClaims uc
                JOIN AspNetUsers u ON uc.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE ul        
                SET ul.UserInternalId = u.InternalId
                FROM AspNetUserLogins ul
                JOIN AspNetUsers u ON ul.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE ur        
                SET ur.UserInternalId = u.InternalId,
                    ur.RoleInternalId = r.InternalId
                FROM AspNetUserRoles ur
                JOIN AspNetUsers u ON ur.UserId = u.Id
                JOIN AspNetRoles r ON ur.RoleId = r.Id
                """);

            migrationBuilder.Sql("""
                UPDATE ut        
                SET ut.UserInternalId = u.InternalId
                FROM AspNetUserTokens ut
                JOIN AspNetUsers u ON ut.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE al        
                SET al.UserInternalId = u.InternalId
                FROM AuditLog al
                JOIN AspNetUsers u ON al.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE p      
                SET p.TeamInternalId = t.InternalId     
                FROM Players p   
                JOIN Teams t ON p.TeamId = t.Id
                """);

            // LEFT join ensures that if a row has no transfer, SourceEntityInternalId is set to null (or remains its current value) rather than being ignored
            migrationBuilder.Sql("""
                UPDATE pv
                SET 
                  pv.PlayerInternalId = p.InternalId,
                  pv.SourceEntityInternalId = t.InternalId
                FROM PlayerValues pv
                JOIN Players p ON pv.PlayerId = p.Id
                LEFT JOIN Transfers t ON pv.SourceEntityId = t.Id
                """);

            migrationBuilder.Sql("""
                UPDATE t
                SET t.OwnerInternalId = u.InternalId
                FROM Teams t
                JOIN AspNetUsers u ON t.OwnerId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE tr
                SET
                  tr.PlayerInternalId = p.InternalId,
                  tr.FromTeamInternalId = ft.InternalId,
                  tr.ToTeamInternalId = tt.InternalId
                FROM Transfers tr
                JOIN Players p ON tr.PlayerId = p.Id
                JOIN Teams ft ON tr.FromTeamId = ft.Id
                LEFT JOIN Teams tt ON tr.ToTeamId = tt.Id
                """);

            migrationBuilder.Sql("""
                UPDATE tbv
                SET
                  tbv.TeamInternalId = t.InternalId,
                  tbv.TransferInternalId = tf.InternalId
                FROM TransferBudgetValues tbv
                JOIN Teams t ON tbv.TeamId = t.Id
                LEFT JOIN Transfers tf ON tbv.TransferId = tf.Id
                """);


            // STEP 3
            DropConstraints(migrationBuilder);


            // STEP 4: Keep the GUID as ExternalId
            migrationBuilder.RenameColumn("Id", "AspNetRoles", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "AspNetRoles", "Id");

            migrationBuilder.RenameColumn("Id", "AspNetUsers", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "AspNetUsers", "Id");

            migrationBuilder.RenameColumn("Id", "AuditLog", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "AuditLog", "Id");

            migrationBuilder.RenameColumn("Id", "Players", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "Players", "Id");

            migrationBuilder.RenameColumn("Id", "PlayerValues", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "PlayerValues", "Id");

            migrationBuilder.RenameColumn("Id", "Teams", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "Teams", "Id");

            migrationBuilder.RenameColumn("Id", "Transfers", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "Transfers", "Id");

            migrationBuilder.RenameColumn("Id", "TransferBudgetValues", "ExternalId");
            migrationBuilder.RenameColumn("InternalId", "TransferBudgetValues", "Id");

            // STEP 5
            RestoreConstraints(migrationBuilder);
        }

        /// <inheritdoc />
        /// <summary>
        /// Reverts the changes made in the Up method, migrating ID columns from <see cref="long"/> back to <see cref="Guid"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Post-Rollback Cleanup Strategy:</b>
        /// After successfully executing <c>Update-Database [Target]</c>, perform the following steps to avoid type mismatch errors:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>Remove <c>ExternalId</c> from C# entity classes.</description>
        /// </item>
        /// <item>
        /// <description>Revert the <c>Id</c> type to <see cref="Guid"/> in entities and <c>IdentityDbContext</c>.</description>
        /// </item>
        /// <item>
        /// <description>Update seeding logic (e.g., <c>HasData</c>) to use GUIDs for the <c>Id</c> field.</description>
        /// </item>
        /// <item>
        /// <description>Delete the migration and designer files for the "bigint migration".</description>
        /// </item>
        /// </list>
        /// <para>
        /// <b>Note:</b> To keep <c>ExternalId</c> in the code without database mapping, use the <c>[NotMapped]</c> attribute:
        /// <code>
        /// [NotMapped]
        /// public Guid ExternalId { get; set; }
        /// </code>
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add GUID columns for foreign keys without making them FKs yet.
            migrationBuilder.AddColumn<Guid>("RoleInternalId", "AspNetRoleClaims", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("UserInternalId", "AspNetUserClaims", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("UserInternalId", "AspNetUserLogins", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>("UserInternalId", "AspNetUserRoles", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>("RoleInternalId", "AspNetUserRoles", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("UserInternalId", "AspNetUserTokens", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("UserInternalId", "AuditLog", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>("TeamInternalId", "Players", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("PlayerInternalId", "PlayerValues", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>("SourceEntityInternalId", "PlayerValues", type: "uniqueidentifier", nullable: true);

            migrationBuilder.AddColumn<Guid>("OwnerInternalId", "Teams", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("FromTeamInternalId", "Transfers", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>("ToTeamInternalId", "Transfers", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>("PlayerInternalId", "Transfers", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>("TeamInternalId", "TransferBudgetValues", type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<Guid>("TransferInternalId", "TransferBudgetValues", type: "uniqueidentifier", nullable: true);

            // STEP 2: Link Entities using the IDs on tables
            migrationBuilder.Sql("""
                UPDATE rc        
                SET rc.RoleInternalId = r.ExternalId
                FROM AspNetRoleClaims rc
                JOIN AspNetRoles r ON rc.RoleId = r.Id
                """);

            migrationBuilder.Sql("""
                UPDATE uc        
                SET uc.UserInternalId = u.ExternalId
                FROM AspNetUserClaims uc
                JOIN AspNetUsers u ON uc.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE ul        
                SET ul.UserInternalId = u.ExternalId
                FROM AspNetUserLogins ul
                JOIN AspNetUsers u ON ul.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE ur        
                SET ur.UserInternalId = u.ExternalId,
                    ur.RoleInternalId = r.ExternalId
                FROM AspNetUserRoles ur
                JOIN AspNetUsers u ON ur.UserId = u.Id
                JOIN AspNetRoles r ON ur.RoleId = r.Id
                """);

            migrationBuilder.Sql("""
                UPDATE ut        
                SET ut.UserInternalId = u.ExternalId
                FROM AspNetUserTokens ut
                JOIN AspNetUsers u ON ut.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE al        
                SET al.UserInternalId = u.ExternalId
                FROM AuditLog al
                JOIN AspNetUsers u ON al.UserId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE p      
                SET p.TeamInternalId = t.ExternalId     
                FROM Players p   
                JOIN Teams t ON p.TeamId = t.Id
                """);

            // LEFT join ensures that if a row has no transfer, SourceEntityInternalId is set to null (or remains its current value) rather than being ignored
            migrationBuilder.Sql("""
                UPDATE pv
                SET
                  pv.PlayerInternalId = p.ExternalId,
                  pv.SourceEntityInternalId = t.ExternalId
                FROM PlayerValues pv
                JOIN Players p ON pv.PlayerId = p.Id
                LEFT JOIN Transfers t ON pv.SourceEntityId = t.Id
                """);

            migrationBuilder.Sql("""
                UPDATE t
                SET t.OwnerInternalId = u.ExternalId
                FROM Teams t
                JOIN AspNetUsers u ON t.OwnerId = u.Id
                """);

            migrationBuilder.Sql("""
                UPDATE tr
                SET
                  tr.PlayerInternalId = p.ExternalId,
                  tr.FromTeamInternalId = ft.ExternalId,
                  tr.ToTeamInternalId = tt.ExternalId
                FROM Transfers tr
                JOIN Players p ON tr.PlayerId = p.Id
                JOIN Teams ft ON tr.FromTeamId = ft.Id
                LEFT JOIN Teams tt ON tr.ToTeamId = tt.Id
                """);

            migrationBuilder.Sql("""
                UPDATE tbv
                SET
                  tbv.TeamInternalId = t.ExternalId,
                  tbv.TransferInternalId = tf.ExternalId
                FROM TransferBudgetValues tbv
                JOIN Teams t ON tbv.TeamId = t.Id
                LEFT JOIN Transfers tf ON tbv.TransferId = tf.Id
                """);


            // STEP 3
            DropConstraints(migrationBuilder);


            // STEP 4: Rename columns back to their original names
            // Rename 'Id' (which is currently the BIGINT) back to 'InternalId'
            // Rename 'ExternalId' (the original GUID) back to 'Id'
            migrationBuilder.RenameColumn("Id", "AspNetUsers", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "AspNetUsers", "Id");

            migrationBuilder.RenameColumn("Id", "AspNetRoles", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "AspNetRoles", "Id");

            migrationBuilder.RenameColumn("Id", "AuditLog", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "AuditLog", "Id");

            migrationBuilder.RenameColumn("Id", "Players", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "Players", "Id");

            migrationBuilder.RenameColumn("Id", "PlayerValues", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "PlayerValues", "Id");

            migrationBuilder.RenameColumn("Id", "Teams", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "Teams", "Id");

            migrationBuilder.RenameColumn("Id", "Transfers", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "Transfers", "Id");

            migrationBuilder.RenameColumn("Id", "TransferBudgetValues", "InternalId");
            migrationBuilder.RenameColumn("ExternalId", "TransferBudgetValues", "Id");


            // Step 5
            RestoreConstraints(migrationBuilder);


            // STEP 6: Remove the BIGINT columns
            // Now that the GUID relationship is restored, delete the BIGINTs
            migrationBuilder.DropColumn("InternalId", "AspNetRoles");
            migrationBuilder.DropColumn("InternalId", "AspNetUsers");
            migrationBuilder.DropColumn("InternalId", "AuditLog");
            migrationBuilder.DropColumn("InternalId", "Players");
            migrationBuilder.DropColumn("InternalId", "PlayerValues");
            migrationBuilder.DropColumn("InternalId", "Teams");
            migrationBuilder.DropColumn("InternalId", "Transfers");
            migrationBuilder.DropColumn("InternalId", "TransferBudgetValues");
        }

        private static void DropConstraints(MigrationBuilder migrationBuilder)
        {
            // Drop Constraints (This will briefly lock the tables)
            // Drop Indexes and FKs first, can't drop PKs without dropping FKs first
            // Drop FKs
            migrationBuilder.DropForeignKey("FK_AspNetRoleClaims_AspNetRoles_RoleId", "AspNetRoleClaims");

            migrationBuilder.DropForeignKey("FK_AspNetUserClaims_AspNetUsers_UserId", "AspNetUserClaims");

            migrationBuilder.DropForeignKey("FK_AspNetUserLogins_AspNetUsers_UserId", "AspNetUserLogins");

            migrationBuilder.DropForeignKey("FK_AspNetUserRoles_AspNetUsers_UserId", "AspNetUserRoles");
            migrationBuilder.DropForeignKey("FK_AspNetUserRoles_AspNetRoles_RoleId", "AspNetUserRoles");

            migrationBuilder.DropForeignKey("FK_AspNetUserTokens_AspNetUsers_UserId", "AspNetUserTokens");

            migrationBuilder.DropForeignKey("FK_AuditLog_AspNetUsers_UserId", "AuditLog");

            migrationBuilder.DropForeignKey("FK_Players_Teams_TeamId", "Players");

            migrationBuilder.DropForeignKey("FK_PlayerValues_Players_PlayerId", "PlayerValues");

            migrationBuilder.DropForeignKey("FK_Teams_AspNetUsers_OwnerId", "Teams");

            migrationBuilder.DropForeignKey("FK_Transfers_Players_PlayerId", "Transfers");
            migrationBuilder.DropForeignKey("FK_Transfers_Teams_FromTeamId", "Transfers");
            migrationBuilder.DropForeignKey("FK_Transfers_Teams_ToTeamId", "Transfers");

            migrationBuilder.DropForeignKey("FK_TransferBudgetValues_Teams_TeamId", "TransferBudgetValues");
            migrationBuilder.DropForeignKey("FK_TransferBudgetValues_Transfers_TransferId", "TransferBudgetValues");

            // Drop Indexes
            migrationBuilder.DropIndex("IX_AspNetRoleClaims_RoleId", "AspNetRoleClaims");

            migrationBuilder.DropIndex("IX_AspNetUserClaims_UserId", "AspNetUserClaims");

            migrationBuilder.DropIndex("IX_AspNetUserLogins_UserId", "AspNetUserLogins");

            migrationBuilder.DropIndex("IX_AspNetUserRoles_RoleId", "AspNetUserRoles");

            migrationBuilder.DropIndex("IX_AuditLog_UserId", "AuditLog");

            migrationBuilder.DropIndex("IX_Players_TeamId", "Players");

            migrationBuilder.DropIndex("IX_PlayerValues_PlayerId", "PlayerValues");

            migrationBuilder.DropIndex("IX_Teams_OwnerId_Name", "Teams");

            migrationBuilder.DropIndex("IX_Transfers_PlayerId", "Transfers");
            migrationBuilder.DropIndex("IX_Transfers_FromTeamId", "Transfers");
            migrationBuilder.DropIndex("IX_Transfers_ToTeamId", "Transfers");

            migrationBuilder.DropIndex("IX_TransferBudgetValues_TeamId", "TransferBudgetValues");
            migrationBuilder.DropIndex("IX_TransferBudgetValues_TransferId", "TransferBudgetValues");

            // Drop PKs
            migrationBuilder.DropPrimaryKey("PK_AspNetRoles", "AspNetRoles");
            migrationBuilder.DropPrimaryKey("PK_AspNetUsers", "AspNetUsers");
            migrationBuilder.DropPrimaryKey("PK_AspNetUserRoles", "AspNetUserRoles");
            migrationBuilder.DropPrimaryKey("PK_AspNetUserTokens", "AspNetUserTokens");
            migrationBuilder.DropPrimaryKey("PK_AuditLog", "AuditLog");
            migrationBuilder.DropPrimaryKey("PK_Players", "Players");
            migrationBuilder.DropPrimaryKey("PK_PlayerValues", "PlayerValues");
            migrationBuilder.DropPrimaryKey("PK_Teams", "Teams");
            migrationBuilder.DropPrimaryKey("PK_Transfers", "Transfers");
            migrationBuilder.DropPrimaryKey("PK_TransferBudgetValues", "TransferBudgetValues");
        }

        private static void RestoreConstraints(MigrationBuilder migrationBuilder)
        {
            // Drop old FK columns and rename new ones to match original names so that app code doesn't need to change
            migrationBuilder.DropColumn("RoleId", "AspNetRoleClaims");
            migrationBuilder.RenameColumn("RoleInternalId", "AspNetRoleClaims", "RoleId");

            migrationBuilder.DropColumn("UserId", "AspNetUserClaims");
            migrationBuilder.RenameColumn("UserInternalId", "AspNetUserClaims", "UserId");

            migrationBuilder.DropColumn("UserId", "AspNetUserLogins");
            migrationBuilder.RenameColumn("UserInternalId", "AspNetUserLogins", "UserId");

            migrationBuilder.DropColumn("UserId", "AspNetUserTokens");
            migrationBuilder.RenameColumn("UserInternalId", "AspNetUserTokens", "UserId");

            migrationBuilder.DropColumn("UserId", "AspNetUserRoles");
            migrationBuilder.RenameColumn("UserInternalId", "AspNetUserRoles", "UserId");
            migrationBuilder.DropColumn("RoleId", "AspNetUserRoles");
            migrationBuilder.RenameColumn("RoleInternalId", "AspNetUserRoles", "RoleId");

            migrationBuilder.DropColumn("UserId", "AuditLog");
            migrationBuilder.RenameColumn("UserInternalId", "AuditLog", "UserId");

            migrationBuilder.DropColumn("TeamId", "Players");
            migrationBuilder.RenameColumn("TeamInternalId", "Players", "TeamId");

            migrationBuilder.DropColumn("PlayerId", "PlayerValues");
            migrationBuilder.RenameColumn("PlayerInternalId", "PlayerValues", "PlayerId");
            migrationBuilder.DropColumn("SourceEntityId", "PlayerValues");
            migrationBuilder.RenameColumn("SourceEntityInternalId", "PlayerValues", "SourceEntityId");

            migrationBuilder.DropColumn("OwnerId", "Teams");
            migrationBuilder.RenameColumn("OwnerInternalId", "Teams", "OwnerId");

            migrationBuilder.DropColumn("PlayerId", "Transfers");
            migrationBuilder.RenameColumn("PlayerInternalId", "Transfers", "PlayerId");
            migrationBuilder.DropColumn("FromTeamId", "Transfers");
            migrationBuilder.RenameColumn("FromTeamInternalId", "Transfers", "FromTeamId");
            migrationBuilder.DropColumn("ToTeamId", "Transfers");
            migrationBuilder.RenameColumn("ToTeamInternalId", "Transfers", "ToTeamId");

            migrationBuilder.DropColumn("TransferId", "TransferBudgetValues");
            migrationBuilder.RenameColumn("TransferInternalId", "TransferBudgetValues", "TransferId");
            migrationBuilder.DropColumn("TeamId", "TransferBudgetValues");
            migrationBuilder.RenameColumn("TeamInternalId", "TransferBudgetValues", "TeamId");

            // Restore PK, indexes and FK
            // Restore PK first, foreign keys depend on PKs, so they must be restored after PKs are in place
            migrationBuilder.AddPrimaryKey("PK_AspNetRoles", "AspNetRoles", "Id");
            migrationBuilder.AddPrimaryKey("PK_AspNetUsers", "AspNetUsers", "Id");
            migrationBuilder.AddPrimaryKey("PK_AspNetUserRoles", "AspNetUserRoles", columns: ["UserId", "RoleId"]);
            migrationBuilder.AddPrimaryKey("PK_AspNetUserTokens", "AspNetUserTokens", columns: ["UserId", "LoginProvider", "Name"]);
            migrationBuilder.AddPrimaryKey("PK_AuditLog", "AuditLog", "Id");
            migrationBuilder.AddPrimaryKey("PK_Players", "Players", "Id");
            migrationBuilder.AddPrimaryKey("PK_PlayerValues", "PlayerValues", "Id");
            migrationBuilder.AddPrimaryKey("PK_Teams", "Teams", "Id");
            migrationBuilder.AddPrimaryKey("PK_Transfers", "Transfers", "Id");
            migrationBuilder.AddPrimaryKey("PK_TransferBudgetValues", "TransferBudgetValues", "Id");

            // Restore Indexes
            migrationBuilder.CreateIndex("IX_AspNetRoleClaims_RoleId", "AspNetRoleClaims", "RoleId");

            migrationBuilder.CreateIndex("IX_AspNetUserClaims_UserId", "AspNetUserClaims", "UserId");

            migrationBuilder.CreateIndex("IX_AspNetUserLogins_UserId", "AspNetUserLogins", "UserId");

            migrationBuilder.CreateIndex("IX_AspNetUserRoles_RoleId", "AspNetUserRoles", "RoleId");

            migrationBuilder.CreateIndex("IX_AuditLog_UserId", "AuditLog", "UserId");

            migrationBuilder.CreateIndex("IX_Players_TeamId", "Players", "TeamId");

            migrationBuilder.CreateIndex("IX_PlayerValues_PlayerId", "PlayerValues", "PlayerId");

            migrationBuilder.CreateIndex(
               name: "IX_Teams_OwnerId_Name",
               table: "Teams",
               columns: ["OwnerId", "Name"],
               unique: true,
               filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex("IX_Transfers_PlayerId", "Transfers", "PlayerId");
            migrationBuilder.CreateIndex("IX_Transfers_FromTeamId", "Transfers", "FromTeamId");
            migrationBuilder.CreateIndex("IX_Transfers_ToTeamId", "Transfers", "ToTeamId");

            migrationBuilder.CreateIndex("IX_TransferBudgetValues_TeamId", "TransferBudgetValues", "TeamId");
            migrationBuilder.CreateIndex("IX_TransferBudgetValues_TransferId", "TransferBudgetValues", "TransferId");

            // Restore FK
            migrationBuilder.AddForeignKey("FK_AspNetRoleClaims_AspNetRoles_RoleId", "AspNetRoleClaims", "RoleId", "AspNetRoles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_AspNetUserClaims_AspNetUsers_UserId", "AspNetUserClaims", "UserId", "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_AspNetUserLogins_AspNetUsers_UserId", "AspNetUserLogins", "UserId", "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_AspNetUserRoles_AspNetUsers_UserId", "AspNetUserRoles", "UserId", "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey("FK_AspNetUserRoles_AspNetRoles_RoleId", "AspNetUserRoles", "RoleId", "AspNetRoles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_AspNetUserTokens_AspNetUsers_UserId", "AspNetUserTokens", "UserId", "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_AuditLog_AspNetUsers_UserId", "AuditLog", "UserId", "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_Players_Teams_TeamId", "Players", "TeamId", "Teams", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_PlayerValues_Players_PlayerId", "PlayerValues", "PlayerId", "Players", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_Teams_AspNetUsers_OwnerId", "Teams", "OwnerId", "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey("FK_Transfers_Players_PlayerId", "Transfers", "PlayerId", "Players", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_Transfers_Teams_FromTeamId", "Transfers", "FromTeamId", "Teams", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_Transfers_Teams_ToTeamId", "Transfers", "ToTeamId", "Teams", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey("FK_TransferBudgetValues_Teams_TeamId", "TransferBudgetValues", "TeamId", "Teams", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey("FK_TransferBudgetValues_Transfers_TransferId", "TransferBudgetValues", "TransferId", "Transfers", principalColumn: "Id");
        }

    }
}
