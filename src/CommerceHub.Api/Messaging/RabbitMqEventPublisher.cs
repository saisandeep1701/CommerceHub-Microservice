using System.Text;
using System.Text.Json;
using CommerceHub.Api.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CommerceHub.Api.Messaging;

public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private bool _disposed;

    public RabbitMqEventPublisher(IOptions<RabbitMqSettings> settings, ILogger<RabbitMqEventPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task PublishAsync<T>(string eventName, T payload) where T : class
    {
        try
        {
            EnsureConnection();

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel!.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = eventName;

            _channel.BasicPublish(
                exchange: _settings.ExchangeName,
                routingKey: eventName,
                basicProperties: properties,
                body: body
            );

            _logger.LogInformation("Published event {EventName} to exchange {Exchange}",
                eventName, _settings.ExchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventName}", eventName);
            throw;
        }

        return Task.CompletedTask;
    }

    private void EnsureConnection()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return;

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange (idempotent)
        _channel.ExchangeDeclare(
            exchange: _settings.ExchangeName,
            type: ExchangeType.Topic,
            durable: true
        );

        _logger.LogInformation("RabbitMQ connection established to {Host}:{Port}",
            _settings.HostName, _settings.Port);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        _disposed = true;
    }
}
