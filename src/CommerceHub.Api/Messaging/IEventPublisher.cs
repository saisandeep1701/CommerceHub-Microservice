namespace CommerceHub.Api.Messaging;

/// <summary>
/// Abstraction for event publishing, enabling testability by
/// allowing tests to mock this interface instead of requiring RabbitMQ.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(string eventName, T payload) where T : class;
}
