using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using PartnerIntegration.Application.Interfaces;
using PartnerIntegration.Domain.Exceptions;
using PartnerIntegration.Infrastructure.Messaging;

namespace PartnerIntegration.Infrastructure.Messaging;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
}

public class RabbitMqMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RabbitMqMessagePublisher(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqMessagePublisher> logger)
    {
        _logger = logger;
        var cfg = settings.Value;

        var factory = new ConnectionFactory
        {
            HostName = cfg.Host,
            Port = cfg.Port,
            UserName = cfg.Username,
            Password = cfg.Password,
            VirtualHost = cfg.VirtualHost,
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _logger.LogInformation("RabbitMQ connection established to {Host}:{Port}", cfg.Host, cfg.Port);
    }

    public Task PublishAsync<T>(T message, string queueName, CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Declare the queue (idempotent)
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var json = JsonSerializer.Serialize(message, JsonOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;  // Survives broker restart
            properties.ContentType = "application/json";
            properties.ContentEncoding = "UTF-8";
            properties.MessageId = Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Message published to queue '{QueueName}' | MessageId: {MessageId}",
                queueName, properties.MessageId);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to queue '{QueueName}'", queueName);
            throw new MessageBrokerException($"Failed to publish message to queue '{queueName}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
