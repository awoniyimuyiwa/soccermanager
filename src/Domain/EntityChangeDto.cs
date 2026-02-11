namespace Domain;

public record EntityChangeDto(
    string EntityName,
    string? NewValues,
    string? OldValues,
    EntityChangeType Type) { }
