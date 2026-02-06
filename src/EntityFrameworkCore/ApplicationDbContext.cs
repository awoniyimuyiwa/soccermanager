using Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore;

class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, long>(options)
{
    public DbSet<Player> Players { get; set; }

    public DbSet<PlayerValue> PlayerValues { get; set; }

    public DbSet<Team> Teams { get; set; }

    public DbSet<Transfer> Transfers { get; set; }

    public DbSet<TransferBudgetValue> TransferBudgetValues { get; set; }

    /// <summary>
    /// Override OnModelCreating to configure entities
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(applicationUser =>
        {
            applicationUser.Property(au => au.FirstName).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.LastName).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.ConcurrencyStamp).HasMaxLength(Constants.StringMaxLength);
            applicationUser.Property(au => au.Id).ValueGeneratedOnAdd();
            applicationUser.HasIndex(au => au.ExternalId)
            .IsUnique();
        });

        modelBuilder.Entity<ApplicationRole>(applicationRole =>
        {
            applicationRole.Property(ar => ar.Id).ValueGeneratedOnAdd();
            applicationRole.HasIndex(ar => ar.ExternalId)
            .IsUnique();
        });

        modelBuilder.Entity<AuditLog>(auditLog =>
        {
            auditLog.Property(al => al.EntityName).HasMaxLength(Constants.StringMaxLength).IsRequired();
        });

        modelBuilder.Entity<Player>(player =>
        {
            player.Property(p => p.Country).HasMaxLength(Constants.StringMaxLength);
            player.Property(p => p.FirstName).HasMaxLength(Constants.StringMaxLength);
            player.Property(p => p.LastName).HasMaxLength(Constants.StringMaxLength);
            player.Property(p => p.Value).HasPrecision(19, 4);
            player.Property(p => p.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Constants.StringMaxLength);

            player.ToTable(p => p.HasCheckConstraint("CK_Player_Value", $"[Value] >= {Constants.MinPlayerValue}"));

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
        });

        modelBuilder.Entity<PlayerValue>(playerValue =>
        {            
            playerValue.Property(pv => pv.Value).HasPrecision(19, 4);
        });

        modelBuilder.Entity<Team>(team =>
        {
            team.Ignore(t => t.AllPlayers);

            team.Property(t => t.Country).HasMaxLength(Constants.StringMaxLength);
            team.Property(t => t.Name).HasMaxLength(Constants.StringMaxLength);
            team.Property(t => t.TransferBudget).HasPrecision(19, 4);
            team.Property(t => t.Value).HasPrecision(19, 4);
            team.Property(t => t.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Constants.StringMaxLength);

            team.ToTable(t => t.HasCheckConstraint("CK_Team_Transfer_Budget", $"[TransferBudget] >= {Constants.MinTeamTransferBudget}"));
            team.ToTable(t => t.HasCheckConstraint("CK_Team_Value", $"[Value] >= {Constants.MinPlayerValue}"));

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
            transfer.Property(t => t.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(Constants.StringMaxLength);

            transfer.ToTable(t => t.HasCheckConstraint("CK_Transfer_Asking_Price_Min", $"[AskingPrice] >= {Constants.MinPlayerAskingPrice}"));

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
            transfer.Navigation(t => t.ToTeam)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

            transfer.Property(t => t.ToTeamId)
                .UsePropertyAccessMode(PropertyAccessMode.Property);
        });

        modelBuilder.Entity<TransferBudgetValue>(transferBudgetValue =>
        {
            transferBudgetValue.Property(tbv => tbv.Description).HasMaxLength(Constants.StringMaxLength);
            transferBudgetValue.Property(tbv => tbv.Value).HasPrecision(19, 4);
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
