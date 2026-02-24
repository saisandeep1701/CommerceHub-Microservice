using CommerceHub.Api.Models;
using MongoDB.Driver;

namespace CommerceHub.Api.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> _orders;

    public OrderRepository(MongoDbContext context)
    {
        _orders = context.Orders;
    }

    public async Task<Order?> GetByIdAsync(string id)
    {
        return await _orders.Find(o => o.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Order> CreateAsync(Order order)
    {
        await _orders.InsertOneAsync(order);
        return order;
    }

    public async Task<Order?> UpdateAsync(string id, Order order)
    {
        var filter = Builders<Order>.Filter.Eq(o => o.Id, id);
        var result = await _orders.ReplaceOneAsync(filter, order);

        return result.ModifiedCount > 0 ? order : null;
    }
}
