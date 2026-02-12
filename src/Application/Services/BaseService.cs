using Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Application.Services
{
    abstract class BaseService(
        IAuditLogManager auditLogManager,
        TimeProvider timeProvider,
        [FromKeyedServices(Constants.AuditLogJsonSerializationOptionsName)] JsonSerializerOptions auditJsonSerializerOptions)
    {
        readonly IAuditLogManager _auditLogManager = auditLogManager;
        readonly TimeProvider _timeProvider = timeProvider;
        readonly JsonSerializerOptions _auditJsonSerializerOptions = auditJsonSerializerOptions;

        protected void LogAction(
            object? parameters = null, 
            [CallerMemberName] string methodName = "")
        {
            if (_auditLogManager.Current is null) { return; }

            var jsonParams = parameters != null
                ? JsonSerializer.Serialize(parameters, _auditJsonSerializerOptions)
                : null;

            _auditLogManager.Current.AuditLogActions.Add(new AuditLogAction
            {
                ExecutionTime = _timeProvider.GetUtcNow(),
                MethodName = methodName,
                Parameters = jsonParams,
                ServiceName = GetType().Name
            });
        }
    }
}
