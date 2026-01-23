namespace Domain;

public class EntityNotFoundException(
    string entityName,
    object entityId) : Exception($"{entityName} with (ID: {entityId}) doesn't exist.")
{
    public string EntityName { get; } = entityName;

    public object EntityId { get; } = entityId;
}
