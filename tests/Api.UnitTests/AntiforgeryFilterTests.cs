using Api.Filters;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace Api.UnitTests;

/// <summary>
/// Unified tests for <see cref="AntiforgeryFilter"/> covering both Controller and Minimal API entry points.
/// </summary>
public class AntiforgeryFilterTests
{
    private const string SuccessFlag = "Success";

    #region Controller Tests (IAsyncAuthorizationFilter)

    [Theory]
    [InlineData("GET", true)]
    [InlineData("POST", false)] // Disabled via metadata
    [InlineData("POST", true, "Bearer some-token")] // Skips for Bearer
    public async Task OnAuthorizationAsync_WhenValidationShouldBeSkipped_DoesNotCallAntiforgery(
        string method, 
        bool requiresValidation, 
        string? authHeader = null)
    {
        // Arrange
        var (context, antiforgeryMock) = GetMvcMocks(method, requiresValidation, authHeader);
        var filter = new AntiforgeryFilter(antiforgeryMock.Object);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_ValidationFails_SetsBadRequestResult()
    {
        // Arrange
        var (context, antiforgeryMock) = GetMvcMocks("POST", true);
        antiforgeryMock.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("Failed"));

        var filter = new AntiforgeryFilter(antiforgeryMock.Object);

        // Act
        await filter.OnAuthorizationAsync(context);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        Assert.Equal(Constants.AntiforgeryValidationErrorMessage, badRequestResult.Value);
    }

    #endregion

    #region Minimal API Tests (IEndpointFilter)

    [Theory]
    [InlineData("GET", true)]
    [InlineData("POST", false)]
    [InlineData("POST", true, "Bearer some-token")]
    public async Task InvokeAsync_WhenValidationShouldBeSkipped_CallsNextDelegate(
        string method, bool requiresValidation, string? authHeader = null)
    {
        // Arrange
        var (context, antiforgeryMock) = GetEndpointMocks(method, requiresValidation, authHeader);
        var filter = new AntiforgeryFilter(antiforgeryMock.Object);
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ => {
            nextCalled = true;
            return ValueTask.FromResult<object?>(SuccessFlag);
        });

        // Assert
        antiforgeryMock.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
        Assert.True(nextCalled);
        Assert.Equal(SuccessFlag, result);
    }

    [Fact]
    public async Task InvokeAsync_ValidationFails_ReturnsBadRequestResult()
    {
        // Arrange
        var (context, antiforgeryMock) = GetEndpointMocks("POST", true);
        antiforgeryMock.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("Failed"));

        var filter = new AntiforgeryFilter(antiforgeryMock.Object);

        // Act
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(SuccessFlag));

        // Assert
        var badRequestResult = Assert.IsType<BadRequest<string>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        Assert.Equal(Constants.AntiforgeryValidationErrorMessage, badRequestResult.Value);
    }

    #endregion

    #region Helpers

    private static (AuthorizationFilterContext, Mock<IAntiforgery>) GetMvcMocks(
        string method, bool requiresValidation, string? authHeader = null)
    {
        var httpContext = CreateHttpContext(method, requiresValidation, authHeader);
        var context = new AuthorizationFilterContext(
            new ActionContext(
                httpContext, 
                new RouteData(), 
                new ActionDescriptor()),
            []);

        return (context, new Mock<IAntiforgery>());
    }

    private static (
        EndpointFilterInvocationContext, 
        Mock<IAntiforgery>) GetEndpointMocks(
        string method,
        bool requiresValidation, 
        string? authHeader = null)
    {
        var httpContext = CreateHttpContext(
            method, 
            requiresValidation, 
            authHeader);
        return (new DefaultEndpointFilterInvocationContext(httpContext), new Mock<IAntiforgery>());
    }

    private static DefaultHttpContext CreateHttpContext(
        string method, 
        bool requiresValidation,
        string? authHeader)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (authHeader != null) context.Request.Headers.Authorization = authHeader;

        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(Mock.Of<IAntiforgeryMetadata>(m => m.RequiresValidation == requiresValidation)),
            "TestEndpoint"));

        return context;
    }

    #endregion
}
