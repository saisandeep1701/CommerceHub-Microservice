namespace CommerceHub.Api.DTOs;

public class CheckoutItemDto
{
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
}

public class CheckoutRequestDto
{
    public string CustomerId { get; set; } = null!;
    public List<CheckoutItemDto> Items { get; set; } = new();
}
