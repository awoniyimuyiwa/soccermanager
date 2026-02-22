using Api.Filters;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace Api.UnitTests;

/// <summary>
/// Contains unit tests for <see cref="AntiforgeryAuthorizationFilter"/>, verifying its behavior in various authorization scenarios.
/// </summary>
/// <remarks>These tests ensure that the AntiforgeryFilter correctly handles cases such as skipping validation
/// when specific attributes or bearer tokens are present, and responds appropriately when antiforgery validation fails.
/// The class uses mock dependencies to simulate HTTP request contexts and filter actions.</remarks>
public class AntiforgeryAuthorizationFilterTests
{
    [Theory]
    [InlineData("DELETE")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("TRACE")]
    public async Task OnAuthorizationAsync_WhenValidationIsDisabled_SkipsValidation(string method)
    {
        await AssertSkipsValidation(
            method,
            false);
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("GET", false)]
    [InlineData("HEAD", true)]
    [InlineData("HEAD", false)]
    [InlineData("OPTIONS", true)]
    [InlineData("OPTIONS", false)]
    [InlineData("TRACE", true)]
    [InlineData("TRACE", false)]
    public async Task OnAuthorizationAsync_WhenMethodIsSafe_SkipsValidation(
        string method,
        bool requiresValidation)
    {
        await AssertSkipsValidation(
            method, 
            requiresValidation);
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task OnAuthorizationAsync_WhenBearerAuthenticatonIsUsed_SkipsValidation(
        string method)
    {
        await AssertSkipsValidation(
           method,
           true,
           "Bearer");
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task OnAuthorizationAsync_WhenBearerAuthenticationIsNotUsedAndValidationIsSuccessful_SetsNullResult(string method)
    {
        // Arrange
        var (authorizationFilterContext,
           antiforgeryMock) = GetMocks(
           method,
           true);
        var antiforgeryFilter = new AntiforgeryAuthorizationFilter(antiforgeryMock.Object);

        // Act
        await antiforgeryFilter.OnAuthorizationAsync(authorizationFilterContext);

        // Assert
        antiforgeryMock.Verify(a => a.ValidateRequestAsync(
            authorizationFilterContext.HttpContext),
            Times.Once);
        // Success implies no result/short-circuit
        Assert.Null(authorizationFilterContext.Result);
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task OnAuthorizationAsync_WhenBearerAuthenticationIsNotUsedAndValidationFails_SetsBadRequestResult(string method)
    {
        // Arrange
        var (authorizationFilterContext,
           antiforgeryMock) = GetMocks(
           method,
           true);

        antiforgeryMock
           .Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
           .ThrowsAsync(new AntiforgeryValidationException("Failed"));
        var antiforgeryFilter = new AntiforgeryAuthorizationFilter(antiforgeryMock.Object);

        // Act
        await antiforgeryFilter.OnAuthorizationAsync(authorizationFilterContext);

        // Assert
        Assert.IsType<BadRequestObjectResult>(authorizationFilterContext.Result);
    }

    private static async Task AssertSkipsValidation(
        string method,
        bool requiresValidation,
        string authorizationMethod = "")
    {
        // Arrange
        var (authorizationFilterContext,
            antiforgeryMock) = GetMocks(
            method,
            requiresValidation,
            authorizationMethod);
        var antiforgeryFilter = new AntiforgeryAuthorizationFilter(antiforgeryMock.Object);

        // Act
        await antiforgeryFilter.OnAuthorizationAsync(authorizationFilterContext);

        // Assert
        antiforgeryMock.Verify(a => a.ValidateRequestAsync(
            It.IsAny<HttpContext>()),
            Times.Never);
        Assert.Null(authorizationFilterContext.Result);
    }

    private static (
        AuthorizationFilterContext authorizationFilterContext,
        Mock<IAntiforgery> antiforgeryMock ) GetMocks(
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

        var authorizationFilterContext = new AuthorizationFilterContext(
            new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()),
            []);

        var antiforgeryMock = new Mock<IAntiforgery>();

        return (
             authorizationFilterContext,
             antiforgeryMock);
    }
}
