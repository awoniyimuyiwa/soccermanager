namespace Domain;

public interface IAuditLogManager
{ 
    AuditLog? Current { get; }
    
    IDisposable BeginScope();

    Task SaveCurrent(CancellationToken cancellationToken = default);
}