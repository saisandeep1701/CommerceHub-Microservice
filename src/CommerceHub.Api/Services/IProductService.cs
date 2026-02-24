using CommerceHub.Api.DTOs;
using CommerceHub.Api.Models;

namespace CommerceHub.Api.Services;

public interface IProductService
{
    Task<ServiceResult<Product>> AdjustStockAsync(string productId, StockAdjustmentDto request);
}
