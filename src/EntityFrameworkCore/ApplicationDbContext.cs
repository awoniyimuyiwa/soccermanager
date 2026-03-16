using Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Text;

namespace EntityFrameworkCore;


/// <summary>
/// The primary database context for the application, handling identity and core business logic.
/// </summary>
/// <param name="options">The options to be used by this <see cref="DbContext"/>.</param>
/// <remarks>
/// When a table has both a Trigger and a Concurrency stamp (RowVersion), SQL Server's 
/// restriction on the <c>OUTPUT</c> clause prevents EF Core from verifying that the 
/// stamp hasn't changed. By adding <c>.HasTrigger()</c> in the model configuration, 
/// EF Core switches to a 'temp table approach' to safely capture the updated 
/// <c>RowVersion</c> while allowing database triggers to fire correctly.
/// </remarks>
class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, long>(options), IDataProtectionKeyContext
{
    public DbSet<AISetting> AISettings => Set<AISetting>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AuditLogAction> AuditLogActions  => Set<AuditLogAction>();

    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    public DbSet<BackgroundServiceStat> BackgroundServiceStats => Set<BackgroundServiceStat>();

    /// <summary>
    /// Stores encrypted keys for .NET data protection (e.g., cookies, tokens).
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<EntityChange> EntityChanges => Set<EntityChange>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerValue> PlayerValues => Set<PlayerValue>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<TransferBudgetValue> TransferBudgetValues => Set<TransferBudgetValue>();

   
    /// <summary>
    /// Override OnModelCreating to configure entities
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AISetting>(aiSettings =>
        {
            aiSettings.Property(a => a.CustomEndpoint).HasMaxLength(Domain.Constants.StringMaxLength);
           
            aiSettings.Property(a => a.Key)   
            .HasColumnType("varbinary(max)") // Optimized for encrypted binary payloads. 
            .HasConversion(  
                // SQL Storage (binary) <- C# Property (Base64Url string)   
                v => v != null ? Base64Url.DecodeFromChars(v) : null,
                    
                // C# Property (Base64Url string) <- SQL Storage (binary)    
                v => v != null ? Base64Url.EncodeToString(v) : null);

            aiSettings.Property(a => a.Provider).HasMaxLength(Domain.Constants.StringMaxLength);

            aiSettings.Property(a => a.Model).HasMaxLength(Domain.Constants.StringMaxLength);
        });

        modelBuilder.Entity<ApplicationUser>(applicationUser =>
        {
            applicationUser.Property(au => au.FirstName).HasMaxLength(Domain.Constants.StringMaxLength);
            applicationUser.Property(au => au.LastName).HasMaxLength(Domain.Constants.StringMaxLength);
            applicationUser.Property(au => au.ConcurrencyStamp).HasMaxLength(Domain.Constants.StringMaxLength);
            applicationUser.Property(au => au.Id).ValueGeneratedOnAdd();
            applicationUser.HasIndex(au => au.ExternalId).IsUnique();

            applicationUser.HasMany<AuditLog>()
            .WithOne(al => al.User)
            .HasForeignKey(al => al.UserId)
            .IsRequired();

            applicationUser.HasOne(u => u.AISetting)
            .WithOne()
            .HasForeignKey<ApplicationUser>(u => u.AISettingId)
            .OnDelete(DeleteBehavior.Cascade);

            // Tell EF to use the property, which triggers your setter logic
            applicationUser.Navigation(u => u.AISetting)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

           applicationUser.Property(u => u.AISettingId)
            .UsePropertyAccessMode(PropertyAccessMode.Property);
        });

        modelBuilder.Entity<ApplicationRole>(applicationRole =>
        {
            applicationRole.Property(ar => ar.Id).ValueGeneratedOnAdd();
            applicationRole.HasIndex(ar => ar.ExternalId)
            .IsUnique();
        });

        modelBuilder.Entity<AuditLogAction>(auditLogAction =>
        {
            auditLogAction.Property(ala => ala.MethodName).HasMaxLength(Domain.Constants.StringMaxLength);
            auditLogAction.Property(ala => ala.ServiceName).HasMaxLength(Domain.Constants.StringMaxLength);
        });

        modelBuilder.Entity<AuditLog>(auditLog =>
        {
            auditLog.Property(al => al.BrowserInfo).HasMaxLength(Domain.Constants.StringMaxLength);
            auditLog.Property(al => al.Exception).HasMaxLength(Domain.Constants.MaxExceptionLength);
            auditLog.Property(al => al.HttpMethod).HasMaxLength(Domain.Constants.StringMaxLength);
            auditLog.Property(al => al.IpAddress).HasMaxLength(45);
            auditLog.Property(al => al.RequestId).HasMaxLength(Domain.Constants.StringMaxLength);
            auditLog.Property(al => al.Url).HasMaxLength(Domain.Constants.StringMaxLength);

            auditLog.HasMany(al => al.AuditLogActions)
            .WithOne()
            .HasForeignKey(aa => aa.AuditLogId)
            .IsRequired();

            auditLog.HasMany(al => al.EntityChanges)
            .WithOne()
            .HasForeignKey(ec => ec.AuditLogId)
            .IsRequired();
        });

        modelBuilder.Entity<BackgroundJob>(backgroundJob =>
        {
            backgroundJob.Property(bj => bj.ConcurrencyStamp).HasMaxLength(Domain.Constants.StringMaxLength);
            backgroundJob.Property(bj => bj.Error).HasMaxLength(Domain.Constants.MaxExceptionLength);

            // Worker Polling
            backgroundJob.HasIndex(bj => new { bj.Status, bj.Priority, bj.ScheduledFor })
             .HasFilter($"[{nameof(BackgroundJob.Status)}] = {(int)BackgroundJobStatus.Queued}");

            // Cleanup Task
            backgroundJob.HasIndex(bj => new { bj.Status, bj.UpdatedAt });

            // General Filtering
            backgroundJob.HasIndex(bj => bj.Type);
        });

        modelBuilder.Entity<BackgroundServiceStat>(backgroundServiceStat =>
        {
            backgroundServiceStat.Property(bss => bss.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Domain.Constants.StringMaxLength);

            backgroundServiceStat
            .HasIndex(bss => bss.Type)
            .IsUnique();
        });

        modelBuilder.Entity<EntityChange>(entityChange =>
        {
            entityChange.Property(ec => ec.EntityName).HasMaxLength(Domain.Constants.StringMaxLength).IsRequired();
        });

        modelBuilder.Entity<Player>(player => 
        {
            player.Property(p => p.Country).HasMaxLength(Domain.Constants.StringMaxLength);
            player.Property(p => p.FirstName).HasMaxLength(Domain.Constants.StringMaxLength);
            player.Property(p => p.LastName).HasMaxLength(Domain.Constants.StringMaxLength);
          
            // ValueGeneratedOnAddOrUpdate() Tells EF to read this back after SaveChanges
            player.Property(p => p.Value)
            .HasPrecision(19, 4)
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValue(0m);  

            player.Property(p => p.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Domain.Constants                                                                    .StringMaxLength);

            player.ToTable(p => p.HasCheckConstraint("CK_Player_Value", $"[Value] >= {Domain.Constants.MinPlayerValue}"));

            player.HasOne(p => p.Team)
            .WithMany("Players")
            .HasForeignKey(p => p.TeamId)
            .IsRequired();

            player.HasMany<PlayerValue>("PlayerValues")
            .WithOne(pv => pv.Player)
            .HasForeignKey(p => p.PlayerId)
            .IsRequired();

            // Tell EF to use the property, which triggers your setter logic
            player.Navigation(p => p.Team)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

            player.Property(p => p.TeamId)
                .UsePropertyAccessMode(PropertyAccessMode.Property);

           // Covering index to speed up trigger
           player.HasIndex(p => p.TeamId)
            .IncludeProperties(p => p.Value);

            player.ToTable(tb => tb.HasTrigger(Constants.TeamValueTriggerName));
        });

        modelBuilder.Entity<PlayerValue>(playerValue =>
        {            
            playerValue.Property(pv => pv.Value)
            .HasPrecision(19, 4)
            .HasDefaultValue(0m);

            // Covering index to speed up trigger
            playerValue.HasIndex(pv => pv.PlayerId)
            .IncludeProperties(pv => pv.Value);

            playerValue.HasIndex(pv => new { pv.Type, pv.SourceEntityId })
            .IsUnique();

            playerValue.ToTable(tb => tb.HasTrigger(Constants.PlayerValueTriggerName));
        });

        modelBuilder.Entity<Team>(team =>
        {
            team.Ignore(t => t.AllPlayers);

            team.Property(t => t.Country).HasMaxLength(Domain.Constants.StringMaxLength);
            team.Property(t => t.Name).HasMaxLength(Domain.Constants.StringMaxLength);
            
            team.Property(t => t.TransferBudget)
            .HasPrecision(19, 4)
            .HasDefaultValue(0m)
            .ValueGeneratedOnAddOrUpdate();
            
            team.Property(t => t.Value)
            .HasPrecision(19, 4)
            .HasDefaultValue(0m)
            .ValueGeneratedOnAddOrUpdate();
           
            team.Property(t => t.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Domain.Constants.StringMaxLength);

            team.ToTable(t => t.HasCheckConstraint(Constants.TeamTransferBudgetCheckConstraintName, $"[TransferBudget] >= {Domain.Constants.MinTeamTransferBudget}"));
            team.ToTable(t => t.HasCheckConstraint("CK_Team_Value", $"[Value] >= {Domain.Constants.MinPlayerValue}"));

            team.HasMany<TransferBudgetValue>("TransferBudgetValues")
            .WithOne(tbv => tbv.Team)
            .HasForeignKey(tbv => tbv.TeamId)
            .IsRequired();

            team.HasIndex(t => new { t.OwnerId, t.Name })
            .IsUnique();
        });

        modelBuilder.Entity<Transfer>(transfer =>
        {
            transfer.Property(t => t.AskingPrice).HasPrecision(19, 4);
            transfer.Property(t => t.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Domain.Constants.StringMaxLength);

            transfer.ToTable(t => t.HasCheckConstraint("CK_Transfer_Asking_Price_Min", $"[AskingPrice] >= {Domain.  Constants.MinPlayerAskingPrice}"));

            transfer.HasOne(t => t.Player) 
            .WithMany("Transfers")       
            .HasForeignKey(t => t.PlayerId)       
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

            transfer.HasOne(t => t.FromTeam)       
            .WithMany("TransfersFrom")       
            .HasForeignKey(t => t.FromTeamId)       
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    
            transfer.HasOne(t => t.ToTeam)      
            .WithMany("TransfersTo")       
            .HasForeignKey(t => t.ToTeamId)       
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

            // Tell EF to use the property, which triggers your setter logic
            transfer.Navigation<Team>(t => t.ToTeam)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

            transfer.Property(t => t.ToTeamId)
                .UsePropertyAccessMode(PropertyAccessMode.Property);
        });

        modelBuilder.Entity<TransferBudgetValue>(transferBudgetValue =>
        {
            transferBudgetValue.Property(tbv => tbv.Description).HasMaxLength(Domain.Constants.StringMaxLength);
            
            transferBudgetValue.Property(tbv => tbv.Value)
            .HasPrecision(19, 4)
            .HasDefaultValue(0m);

            // Covering index to speed up trigger
            transferBudgetValue.HasIndex(tbv => tbv.TeamId)
            .IncludeProperties(tbv => tbv.Value);

            transferBudgetValue.HasIndex(tbv => tbv.TransferId)
            .IsUnique();

            transferBudgetValue.ToTable(tb => tb.HasTrigger(Constants.TeamTransferBudgetTriggerName));
        });

        // Find all entity types that are a subclass of Entity
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(Entity).IsAssignableFrom(e.ClrType) && !e.ClrType.IsAbstract);

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property("Id") // Use string if Id is defined in the base class
                .UseIdentityColumn();

            modelBuilder.Entity(entityType.ClrType)
                .HasIndex("ExternalId")
                .IsUnique();
        }
    }
}
