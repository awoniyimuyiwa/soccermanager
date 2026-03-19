using Application.Attributes;
using Application.Contracts.BackgroundJobs;
using Application.Extensions;
using Domain.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApplicationServices_AddsJobHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplicationServices();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IBackgroundJobRunner) && d.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, d => d.ServiceType == typeof(IBackgroundJobTypeRegistry) && d.Lifetime == ServiceLifetime.Singleton);

        // Verify every Enum value has exactly one Scoped registration
        var provider = services.BuildServiceProvider();
        foreach (var jobType in Enum.GetValues<BackgroundJobType>())
        {
            var handler = provider.GetKeyedService<IBackgroundJobHandler>(jobType);
            Assert.NotNull(handler);

            // .Single() ensures exactly one registration exists
            var descriptor = services.Single(
                d => d.ServiceType == typeof(IBackgroundJobHandler)
                     && Equals(d.ServiceKey, jobType));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        var handlerTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IBackgroundJobHandler).IsAssignableFrom(t)
                        && t.IsClass
                        && !t.IsAbstract
                        && !t.IsGenericType);
        foreach (var type in handlerTypes)
        {
            // Must have the attribute
            Assert.True(type.IsDefined(typeof(BackgroundJobHandlerAttribute)),
                $"Handler '{type.Name}' is missing the [BackgroundJobHandler] attribute.");

            // Must be registered as the implementation for the Keyed Service
            var isRegistered = services.Any(x =>
                x.ServiceType == typeof(IBackgroundJobHandler) &&
                x.ImplementationType == type &&
                x.ServiceKey != null); // Ensures it has a key

            Assert.True(isRegistered,
                $"Handler '{type.Name}' was found in assembly but is not registered as a Keyed Service.");
        }
    }
}