using CommerceHub.Api.Models;

namespace CommerceHub.Api.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string id);
    Task<Order> CreateAsync(Order order);
    Task<Order?> UpdateAsync(string id, Order order);
}
