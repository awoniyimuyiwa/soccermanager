using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;
using System.Reflection;

namespace Api.Extensions;

public static class IConnectionMultiplexerExtensions
{
    public static IConnectionMultiplexer WithKeyPrefix(
        this IConnectionMultiplexer multiplexer, 
        string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return multiplexer;
        return GetDatabaseWithKeyPrefixProxy.Decorate(multiplexer, prefix);
    }

    private class GetDatabaseWithKeyPrefixProxy : DispatchProxy
    {
        private IConnectionMultiplexer _target = null!;
        private string _prefix = null!;

        public static IConnectionMultiplexer Decorate(
            IConnectionMultiplexer target,
            string prefix)
        {
            var proxy = Create<IConnectionMultiplexer, GetDatabaseWithKeyPrefixProxy>() as GetDatabaseWithKeyPrefixProxy;
            proxy!._target = target;
            proxy!._prefix = prefix;
            return (proxy as IConnectionMultiplexer)!;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var result = targetMethod?.Invoke(_target, args);

            // Intercept 'GetDatabase' calls to apply the prefix
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase) && result is IDatabase db)
            {
                return db.WithKeyPrefix(_prefix);
            }

            return result;
        }
    }
}


