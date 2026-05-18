using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using PartnerIntegration.API.Middleware;
using PartnerIntegration.Domain.Exceptions;
using Xunit;
using ValidationException = PartnerIntegration.Domain.Exceptions.ValidationException;

namespace PartnerIntegration.Tests.Unit.Services;

public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionHandlerMiddleware>> _loggerMock;

    public GlobalExceptionHandlerMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
    }

    private async Task<(int StatusCode, string Body)> InvokeMiddleware(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw exception,
            _loggerMock.Object);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task Handle_ValidationException_ShouldReturn422()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Amount"] = ["Amount must be greater than 0."]
        };
        var ex = new ValidationException(errors);

        var (statusCode, body) = await InvokeMiddleware(ex);

        statusCode.Should().Be(422);
        body.Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Handle_PartnerVerificationException_ShouldReturn502()
    {
        var ex = new PartnerVerificationException("P-1001");

        var (statusCode, body) = await InvokeMiddleware(ex);

        statusCode.Should().Be(502);
        body.Should().Contain("PARTNER_VERIFICATION_FAILED");
    }

    [Fact]
    public async Task Handle_MessageBrokerException_ShouldReturn503()
    {
        var ex = new MessageBrokerException("Connection refused");

        var (statusCode, body) = await InvokeMiddleware(ex);

        statusCode.Should().Be(503);
        body.Should().Contain("MESSAGE_BROKER_UNAVAILABLE");
    }

    [Fact]
    public async Task Handle_UnhandledException_ShouldReturn500()
    {
        var ex = new Exception("Unexpected error");

        var (statusCode, body) = await InvokeMiddleware(ex);

        statusCode.Should().Be(500);
        body.Should().Contain("INTERNAL_SERVER_ERROR");
    }

    [Fact]
    public async Task Handle_OperationCancelledException_ShouldReturn408()
    {
        var ex = new OperationCanceledException();

        var (statusCode, body) = await InvokeMiddleware(ex);

        statusCode.Should().Be(408);
        body.Should().Contain("REQUEST_TIMEOUT");
    }

    [Fact]
    public async Task Handle_AnyException_ResponseShouldBeJson()
    {
        var ex = new Exception("Test");

        var (_, body) = await InvokeMiddleware(ex);

        var act = () => JsonDocument.Parse(body);
        act.Should().NotThrow("response body should be valid JSON");
    }

    [Fact]
    public async Task Handle_ValidationException_ShouldIncludeValidationErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Amount"] = ["Amount must be greater than 0.", "Amount is required."],
            ["Currency"] = ["Currency is not valid."]
        };
        var ex = new ValidationException(errors);

        var (_, body) = await InvokeMiddleware(ex);

        var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("validationErrors", out var validationErrors).Should().BeTrue();
        validationErrors.TryGetProperty("Amount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoException_ShouldCallNextMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _loggerMock.Object);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
