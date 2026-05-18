using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PartnerIntegration.Infrastructure.ExternalServices;
using System.Net;
using System.Text.Json;
using Xunit;
using PartnerIntegration.Application.DTOs;
using PartnerIntegration.Domain.Exceptions;

namespace PartnerIntegration.Tests.Unit.Services;

public class PartnerVerificationServiceTests
{
    private readonly Mock<ILogger<PartnerVerificationService>> _loggerMock;

    public PartnerVerificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<PartnerVerificationService>>();
    }

    private PartnerVerificationService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://test-host")
        };
        return new PartnerVerificationService(client, _loggerMock.Object);
    }

    private static HttpMessageHandler CreateSuccessHandler(string partnerId)
    {
        var response = new PartnerVerificationResponse(partnerId, true, "Test Partner");
        var json = JsonSerializer.Serialize(response);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });

        return handlerMock.Object;
    }

    private static HttpMessageHandler CreateFailureHandler(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode });

        return handlerMock.Object;
    }

    private static HttpMessageHandler CreateTimeoutHandler()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout", new TimeoutException()));

        return handlerMock.Object;
    }

    [Fact]
    public async Task VerifyPartner_SuccessfulResponse_ShouldReturnVerification()
    {
        var sut = CreateService(CreateSuccessHandler("P-1001"));

        var result = await sut.VerifyPartnerAsync("P-1001");

        result.Should().NotBeNull();
        result.PartnerId.Should().Be("P-1001");
        result.IsActive.Should().BeTrue();
        result.PartnerName.Should().Be("Test Partner");
    }

    [Fact]
    public async Task VerifyPartner_NotFoundResponse_ShouldThrowPartnerVerificationException()
    {
        var sut = CreateService(CreateFailureHandler(HttpStatusCode.NotFound));

        var act = () => sut.VerifyPartnerAsync("P-UNKNOWN");

        await act.Should().ThrowAsync<PartnerVerificationException>();
    }

    [Fact]
    public async Task VerifyPartner_ServerError_ShouldThrowPartnerVerificationException()
    {
        var sut = CreateService(CreateFailureHandler(HttpStatusCode.InternalServerError));

        var act = () => sut.VerifyPartnerAsync("P-1001");

        await act.Should().ThrowAsync<PartnerVerificationException>();
    }

    [Fact]
    public async Task VerifyPartner_Timeout_ShouldThrowPartnerVerificationException()
    {
        var sut = CreateService(CreateTimeoutHandler());

        var act = () => sut.VerifyPartnerAsync("P-1001");

        await act.Should().ThrowAsync<PartnerVerificationException>();
    }

    [Fact]
    public async Task VerifyPartner_CallsCorrectEndpoint()
    {
        var capturedRequest = (HttpRequestMessage?)null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new PartnerVerificationResponse("P-1001", true, "Test")),
                    System.Text.Encoding.UTF8,
                    "application/json")
            });

        var sut = CreateService(handlerMock.Object);

        await sut.VerifyPartnerAsync("P-1001");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.PathAndQuery
            .Should().Be("/api/v1/mock/partners/P-1001/verify");
    }
}
