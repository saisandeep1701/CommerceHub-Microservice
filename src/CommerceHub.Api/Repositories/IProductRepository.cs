using CommerceHub.Api.Models;

namespace CommerceHub.Api.Repositories;

public interface IProductRepository
{
    /// <summary>
    /// Gets a product by its ID.
    /// </summary>
    Task<Product?> GetByIdAsync(string id);

    /// <summary>
    /// Atomically decrements stock for a product. Returns the updated product if
    /// sufficient stock was available, or null if stock was insufficient or product not found.
    /// Uses MongoDB findOneAndUpdate with a conditional filter (stock >= quantity).
    /// </summary>
    Task<Product?> DecrementStockAsync(string productId, int quantity);

    /// <summary>
    /// Atomically increments stock for a product (used for rollback).
    /// </summary>
    Task IncrementStockAsync(string productId, int quantity);

    /// <summary>
    /// Atomically adjusts stock by a delta. For negative deltas, ensures stock won't go below 0.
    /// Returns the updated product, or null if the adjustment would result in negative stock.
    /// </summary>
    Task<Product?> AdjustStockAsync(string productId, int adjustment);
}
