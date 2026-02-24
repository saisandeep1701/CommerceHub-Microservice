namespace CommerceHub.Api.Models;

/// <summary>
/// Represents the lifecycle status of an order.
/// </summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Cancelled
}
