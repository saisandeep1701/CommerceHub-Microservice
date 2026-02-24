using CommerceHub.Api.Models;

namespace CommerceHub.Api.DTOs;

public class UpdateOrderItemDto
{
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class UpdateOrderDto
{
    public string CustomerId { get; set; } = null!;
    public List<UpdateOrderItemDto> Items { get; set; } = new();
    public string Status { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}
