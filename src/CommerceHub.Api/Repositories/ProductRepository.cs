using CommerceHub.Api.Models;
using MongoDB.Driver;

namespace CommerceHub.Api.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IMongoCollection<Product> _products;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(MongoDbContext context, ILogger<ProductRepository> logger)
    {
        _products = context.Products;
        _logger = logger;
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        return await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Atomic stock decrement using findOneAndUpdate with a guard condition.
    /// The filter ensures we only match if Stock >= quantity, preventing negative stock.
    /// </summary>
    public async Task<Product?> DecrementStockAsync(string productId, int quantity)
    {
        var filter = Builders<Product>.Filter.And(
            Builders<Product>.Filter.Eq(p => p.Id, productId),
            Builders<Product>.Filter.Gte(p => p.Stock, quantity)
        );

        var update = Builders<Product>.Update
            .Inc(p => p.Stock, -quantity)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<Product>
        {
            ReturnDocument = ReturnDocument.After
        };

        var result = await _products.FindOneAndUpdateAsync(filter, update, options);

        if (result == null)
        {
            _logger.LogWarning("Failed to decrement stock for product {ProductId} by {Quantity} — insufficient stock or product not found",
                productId, quantity);
        }

        return result;
    }

    /// <summary>
    /// Unconditional stock increment used for rollback during checkout failure.
    /// </summary>
    public async Task IncrementStockAsync(string productId, int quantity)
    {
        var filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        var update = Builders<Product>.Update
            .Inc(p => p.Stock, quantity)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _products.UpdateOneAsync(filter, update);
    }

    /// <summary>
    /// Atomic stock adjustment for the PATCH endpoint.
    /// For negative adjustments, uses a guard filter to prevent negative stock.
    /// For positive adjustments, no guard is needed.
    /// </summary>
    public async Task<Product?> AdjustStockAsync(string productId, int adjustment)
    {
        FilterDefinition<Product> filter;

        if (adjustment < 0)
        {
            // Guard: only match if current stock can absorb the decrement
            filter = Builders<Product>.Filter.And(
                Builders<Product>.Filter.Eq(p => p.Id, productId),
                Builders<Product>.Filter.Gte(p => p.Stock, Math.Abs(adjustment))
            );
        }
        else
        {
            filter = Builders<Product>.Filter.Eq(p => p.Id, productId);
        }

        var update = Builders<Product>.Update
            .Inc(p => p.Stock, adjustment)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<Product>
        {
            ReturnDocument = ReturnDocument.After
        };

        return await _products.FindOneAndUpdateAsync(filter, update, options);
    }
}
