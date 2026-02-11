using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace EntityFrameworkCore.Interceptors;

class CustomSaveChangesInterceptor(
    IAuditLogManager auditLogManager,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    readonly IAuditLogManager _auditLogManager = auditLogManager;
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

            if (entry.Entity is AuditedEntity
                && entry.State == EntityState.Added
                || entry.State == EntityState.Modified
                || entry.State == EntityState.Deleted)
            {
                _auditLogManager.Current?.EntityChanges.Add(new EntityChange    
                {
                    EntityId = (long)entry.Property("Id").CurrentValue!,

                    EntityName = entry.Entity.GetType().Name,

                    // Only capture modified properties for Updates, but log all properties for Adds and Deletes
                    OldValues = SerializeProperties(entry, true)!,
                   
                    NewValues = SerializeProperties(entry, false),
 
                    Type = entry.State switch
                    { 
                        EntityState.Added => EntityChangeType.Created,
                        EntityState.Modified => EntityChangeType.Updated,
                        EntityState.Deleted => EntityChangeType.Deleted,
                         _ => throw new NotSupportedException($"Unsupported entity state: {entry.State}")
                    }
                });
            }
        }
    }

    private static string? SerializeProperties(EntityEntry entry, bool isOld)
    {
        var properties = entry.Properties
            .Where(p =>
            {
                // Only log modified properties for Updates
                return entry.State == EntityState.Added
                       || entry.State == EntityState.Deleted
                       || p.IsModified;
            })
            .ToDictionary(
            p => p.Metadata.Name,
            p =>
            {
                // Ignore large binary data (byte arrays)
                if (p.Metadata.ClrType == typeof(byte[])
                    || p.Metadata.ClrType == typeof(Stream))
                {
                    return Domain.Constants.BinaryDataMask;
                }

                // Mask sensitive fields and fields marked as not audited
                var propertyInfo = p.Metadata.PropertyInfo;
                if (Domain.Constants.SensitiveFieldNames.Contains(p.Metadata.Name)
                    || (propertyInfo != null && Attribute.IsDefined(propertyInfo, typeof(NotAuditedAttribute))))
                {
                    return Domain.Constants.Mask;
                }

                // Return the actual value
                var val = isOld ? p.OriginalValue : p.CurrentValue;

                // Trim long strings
                if (val is string s && s.Length > Domain.Constants.StringMaxLength)
                    return string.Concat(
                        s.AsSpan(0, Domain.Constants.StringMaxLength - Domain.Constants.TruncationIndicator.Length), 
                        Domain.Constants.TruncationIndicator);

                return val;
            });

        return properties.Count != 0
            ? JsonSerializer.Serialize(properties) : null;
    }
}
