using CommerceHub.Api.DTOs;
using CommerceHub.Api.Models;
using CommerceHub.Api.Repositories;

namespace CommerceHub.Api.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository productRepository, ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<Product>> AdjustStockAsync(string productId, StockAdjustmentDto request)
    {
        // First check if product exists to give a useful error
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
        {
            return ServiceResult<Product>.Fail(404, $"Product with ID '{productId}' not found.");
        }

        var updatedProduct = await _productRepository.AdjustStockAsync(productId, request.Adjustment);

        if (updatedProduct == null)
        {
            _logger.LogWarning(
                "Stock adjustment rejected for product {ProductId}. Adjustment: {Adjustment}, Current stock: {Stock}",
                productId, request.Adjustment, product.Stock);
            return ServiceResult<Product>.Fail(409,
                $"Stock adjustment would result in negative stock. " +
                $"Current stock: {product.Stock}, Requested adjustment: {request.Adjustment}.");
        }

        _logger.LogInformation("Stock adjusted for product {ProductId}: {Adjustment}. New stock: {Stock}",
            productId, request.Adjustment, updatedProduct.Stock);

        return ServiceResult<Product>.Ok(updatedProduct);
    }
}
