namespace Domain;

public class ConcurrencyException(
    string entityName,
    object entityId,
    object? dbValues = null) : Exception($"Concurrency conflict detected on {entityName} (ID: {entityId}).")
{
    public string EntityName { get; } = entityName;

    public object EntityId { get; } = entityId;

    public object? CurrentDatabaseValues { get; } = dbValues;
}
