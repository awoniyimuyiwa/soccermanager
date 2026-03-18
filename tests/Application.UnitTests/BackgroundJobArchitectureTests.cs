using Application.Attributes;
using Application.Contracts;
using Application.Extensions;
using Domain;
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

        // Verify BackgroundJobRunner is Transient
        var runnerDescriptor = services.Single(x => x.ServiceType == typeof(IBackgroundJobRunner));
        Assert.Equal(ServiceLifetime.Transient, runnerDescriptor.Lifetime);

        // Verify every Enum value has exactly one Scoped registration
        var provider = services.BuildServiceProvider();
        foreach (var jobType in Enum.GetValues<BackgroundJobType>())
        {
            var handler = provider.GetKeyedService<IBackgroundJobHandler>(jobType);
            Assert.NotNull(handler);

            // .Single() ensures exactly one registration exists
            var descriptor = services.Single(
                x => x.ServiceType == typeof(IBackgroundJobHandler)
                     && Equals(x.ServiceKey, jobType));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        // Verify handlers are not registered as concrete types (Interface only)
        var handlerTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IBackgroundJobHandler).IsAssignableFrom(t)
                        && t.IsClass
                        && !t.IsAbstract
                        && !t.IsGenericType);
        foreach (var type in handlerTypes)
        {
            var exists = services.Any(x => x.ServiceType == type);
            Assert.False(exists, $"Handler '{type.Name}' should only be registered via its interface.");
        }

        // Verify every implementation is decorated with the required attribute
        foreach (var type in handlerTypes)
        {
            var hasAttribute = type.IsDefined(typeof(BackgroundJobHandlerAttribute), false);
            Assert.True(hasAttribute, $"Handler '{type.Name}' is missing the [BackgroundJobHandler] attribute.");
        }
    }
}


