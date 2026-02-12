using Domain;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore;

class AuditLogManager(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IAuditLogManager
{ 
    readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    readonly TimeProvider _timeProvider = timeProvider;

    AuditLog? _current;
    public AuditLog? Current => _current;

    public IDisposable BeginScope()
    {
        _current = new AuditLog
        {
            ExternalId = Guid.NewGuid(),
            TimeStamp = _timeProvider.GetUtcNow()
        };

        // Return a disposable to clear the scope when finished
        return new DisposeAction(() => _current = null);
    }

    public async Task SaveCurrent(CancellationToken cancellationToken = default)
    {
        if (_current is null) { return; }

        using var scope = _scopeFactory.CreateScope();
        var applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        applicationDbContext.AuditLogs.Add(_current);
        await applicationDbContext.SaveChangesAsync(cancellationToken);
    }
}
