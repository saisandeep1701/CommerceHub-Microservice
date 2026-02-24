namespace CommerceHub.Api.DTOs;

public class StockAdjustmentDto
{
    /// <summary>
    /// The quantity to adjust. Positive to add stock, negative to remove stock.
    /// </summary>
    public int Adjustment { get; set; }
}
