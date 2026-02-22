using Api.Filters;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace Api.UnitTests;

/// <summary>
/// Contains unit tests for <see cref="AntiforgeryEndpointFilter"/>, verifying its behavior in various authorization scenarios.
/// </summary>
/// <remarks>These tests ensure validation is skipped when necessary
/// when specific attributes or bearer tokens are present, and responds appropriately when antiforgery validation fails.
/// The class uses mock dependencies to simulate HTTP request contexts and filter actions.</remarks>
public class AntiforgeryEndpointFilterTests
{
    [Theory]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task OnInvokeAsync_WhenBearerAuthenticationIsNotUsedAndValidationFails_ReturnsBadRequestResult(string method)
    {
        // Arrange
        var (endpointFiterInvocationContext, 
            antiforgeryMock) = GetMocks(
            method, 
            true);

        antiforgeryMock
            .Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("Failed"));

        static ValueTask<object?> next(EndpointFilterInvocationContext _) => ValueTask.FromResult<object?>("Success");
        var endpointFilter = new AntiforgeryEndpointFilter(antiforgeryMock.Object);
     
        // Act
        var result = await endpointFilter.InvokeAsync(endpointFiterInvocationContext, next);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<BadRequest<string>>(result);
    }

    private static (
        DefaultEndpointFilterInvocationContext endpointFiterInvocationContext, 
        Mock<IAntiforgery> antiforgeryMock) GetMocks(
        string method,
        bool requiresValidation,
        string authorizationMethod = "")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Headers.Authorization = authorizationMethod;
        httpContext.SetEndpoint(new Endpoint(
            null,
            new EndpointMetadataCollection(Mock.Of<IAntiforgeryMetadata>(m => m.RequiresValidation == requiresValidation)),
           "Test"));

        var antiforgeryMock = new Mock<IAntiforgery>();

        var endpointFiterInvocationContext = new DefaultEndpointFilterInvocationContext(httpContext);
        
        return (
            endpointFiterInvocationContext,
            antiforgeryMock);
    }
}
