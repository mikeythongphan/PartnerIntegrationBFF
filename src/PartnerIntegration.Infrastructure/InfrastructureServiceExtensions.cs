using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PartnerIntegration.Application.Interfaces;
using PartnerIntegration.Application.Queries.GetTransactionStatus;
using PartnerIntegration.Infrastructure.ExternalServices;
using PartnerIntegration.Infrastructure.Messaging;
using PartnerIntegration.Infrastructure.ReadStores;
using Polly;

namespace PartnerIntegration.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // RabbitMQ
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

        // Read/Write store (singleton — shared ConcurrentDictionary)
        services.AddSingleton<InMemoryTransactionReadStore>();
        services.AddSingleton<ITransactionReadStore>(sp => sp.GetRequiredService<InMemoryTransactionReadStore>());
        services.AddSingleton<ITransactionWriteStore>(sp => sp.GetRequiredService<InMemoryTransactionReadStore>());

        // Partner Verification HTTP Client with Polly resilience
        services.AddHttpClient<IPartnerVerificationService, PartnerVerificationService>(client =>
            {
                // Points to the mock endpoint (same host in this case)
                var baseUrl = configuration["PartnerVerificationApi:BaseUrl"] ?? "http://localhost:5000";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddResilienceHandler("partner-verification-pipeline", builder =>
            {
                // Retry: 3 attempts with exponential backoff
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = static args =>
                        ValueTask.FromResult(
                            args.Outcome.Exception is TimeoutException
                            || args.Outcome.Exception is HttpRequestException
                            || (args.Outcome.Result?.StatusCode >= System.Net.HttpStatusCode.InternalServerError))
                });

                // Circuit Breaker: opens after 5 failures in 30s window
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    FailureRatio = 0.5,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });

                // Per-attempt timeout
                builder.AddTimeout(TimeSpan.FromSeconds(5));
            });

        return services;
    }
}
