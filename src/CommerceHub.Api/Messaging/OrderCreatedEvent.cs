namespace CommerceHub.Api.Messaging;

/// <summary>
/// Event payload published to RabbitMQ when an order is successfully created.
/// </summary>
public class OrderCreatedEvent
{
    public string OrderId { get; set; } = null!;
    public string CustomerId { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
