using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.Interceptors;

class CustomSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    readonly TimeProvider _timeProvider = timeProvider;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
         InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var utcNow = _timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries())
        {
             bool hasMeaningfulChange = entry.State == EntityState.Added
                || entry.Properties.Any(p => p.IsModified);

            if (entry.Entity is AuditedEntity auditedEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    // Preserve any explicitly set CreatedAt value
                    if (auditedEntity.CreatedAt == default)
                    {
                        auditedEntity.CreatedAt = utcNow;
                    }

                    auditedEntity.UpdatedAt = utcNow;
                    // For added entities, EF will insert these values; no need to force property modified flags
                }
                else if (entry.State == EntityState.Modified && hasMeaningfulChange)
                {
                    auditedEntity.UpdatedAt = utcNow;

                    // If the entry exposes an "UpdatedAt" property, ensure EF knows it changed
                    var updatedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                    if (updatedAtProperty != null)
                    {
                        updatedAtProperty.CurrentValue = auditedEntity.UpdatedAt;
                        updatedAtProperty.IsModified = true;
                    }
                }
            }
           
            if (entry.Entity is IHasConcurrencyStamp hasConcurrencyStamp)
            {
                if (entry.State == EntityState.Added)
                {
                    hasConcurrencyStamp.ConcurrencyStamp = Guid.NewGuid().ToString();
                }
                else if (entry.State == EntityState.Modified && hasMeaningfulChange)
                {
                    var concurrencyStampProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "ConcurrencyStamp");
                   
                    // If a tracked property exists, override the OriginalValue with value from user input so EF core includes it in the WHERE clause
                    if (concurrencyStampProperty is not null)
                    {                      
                        concurrencyStampProperty.OriginalValue = concurrencyStampProperty.CurrentValue;
                    }

                    hasConcurrencyStamp.ConcurrencyStamp = Guid.NewGuid().ToString();               
                    // If a tracked property exists, ensure its current value is synced (not strictly required)
                    if (concurrencyStampProperty is not null)
                    {
                        concurrencyStampProperty.CurrentValue = hasConcurrencyStamp.ConcurrencyStamp;
                        concurrencyStampProperty.IsModified = true;
                    }
                }
            }
        }
    }
}
