using CommerceHub.Api.DTOs;
using CommerceHub.Api.Messaging;
using CommerceHub.Api.Models;
using CommerceHub.Api.Repositories;

namespace CommerceHub.Api.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IEventPublisher eventPublisher,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Processes a checkout by atomically decrementing stock for each item,
    /// creating the order, and publishing an OrderCreated event.
    /// If any stock decrement fails, previously decremented items are rolled back.
    /// </summary>
    public async Task<ServiceResult<OrderResponseDto>> CheckoutAsync(CheckoutRequestDto request)
    {
        var decrementedItems = new List<(string ProductId, int Quantity)>();
        var orderItems = new List<OrderItem>();

        try
        {
            // Phase 1: Atomically decrement stock for each item
            foreach (var item in request.Items)
            {
                var product = await _productRepository.DecrementStockAsync(item.ProductId, item.Quantity);

                if (product == null)
                {
                    // Check if product exists at all to give a more specific error
                    var existingProduct = await _productRepository.GetByIdAsync(item.ProductId);
                    if (existingProduct == null)
                    {
                        _logger.LogWarning("Checkout failed: Product {ProductId} not found", item.ProductId);
                        await RollbackStockAsync(decrementedItems);
                        return ServiceResult<OrderResponseDto>.Fail(404,
                            $"Product with ID '{item.ProductId}' not found.");
                    }

                    _logger.LogWarning("Checkout failed: Insufficient stock for product {ProductId}. Requested: {Quantity}, Available: {Stock}",
                        item.ProductId, item.Quantity, existingProduct.Stock);
                    await RollbackStockAsync(decrementedItems);
                    return ServiceResult<OrderResponseDto>.Fail(409,
                        $"Insufficient stock for product '{existingProduct.Name}' (SKU: {existingProduct.Sku}). " +
                        $"Requested: {item.Quantity}, Available: {existingProduct.Stock}.");
                }

                decrementedItems.Add((item.ProductId, item.Quantity));
                orderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });
            }

            // Phase 2: Create the order
            var order = new Order
            {
                CustomerId = request.CustomerId,
                Items = orderItems,
                Status = OrderStatus.Pending,
                TotalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdOrder = await _orderRepository.CreateAsync(order);

            // Phase 3: Publish event ONLY after successful order creation
            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = createdOrder.Id,
                CustomerId = createdOrder.CustomerId,
                TotalAmount = createdOrder.TotalAmount,
                ItemCount = createdOrder.Items.Count,
                CreatedAt = createdOrder.CreatedAt
            };

            await _eventPublisher.PublishAsync("order.created", orderCreatedEvent);

            _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId}. Total: {Total}",
                createdOrder.Id, createdOrder.CustomerId, createdOrder.TotalAmount);

            return ServiceResult<OrderResponseDto>.Created(OrderResponseDto.FromOrder(createdOrder));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during checkout. Rolling back stock decrements.");
            await RollbackStockAsync(decrementedItems);
            throw;
        }
    }

    public async Task<ServiceResult<OrderResponseDto>> GetByIdAsync(string id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order == null)
        {
            return ServiceResult<OrderResponseDto>.Fail(404, $"Order with ID '{id}' not found.");
        }

        return ServiceResult<OrderResponseDto>.Ok(OrderResponseDto.FromOrder(order));
    }

    public async Task<ServiceResult<OrderResponseDto>> UpdateAsync(string id, UpdateOrderDto request)
    {
        var existingOrder = await _orderRepository.GetByIdAsync(id);
        if (existingOrder == null)
        {
            return ServiceResult<OrderResponseDto>.Fail(404, $"Order with ID '{id}' not found.");
        }

        // Block updates if order is already Shipped
        if (existingOrder.Status == OrderStatus.Shipped)
        {
            return ServiceResult<OrderResponseDto>.Fail(409,
                "Cannot update an order that has already been shipped.");
        }

        // Parse and validate the new status
        if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return ServiceResult<OrderResponseDto>.Fail(400,
                $"Invalid status: '{request.Status}'. Valid values: Pending, Paid, Shipped, Cancelled.");
        }

        // Apply full replacement
        existingOrder.CustomerId = request.CustomerId;
        existingOrder.Items = request.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList();
        existingOrder.Status = newStatus;
        existingOrder.TotalAmount = request.TotalAmount;
        existingOrder.UpdatedAt = DateTime.UtcNow;

        var updatedOrder = await _orderRepository.UpdateAsync(id, existingOrder);
        if (updatedOrder == null)
        {
            return ServiceResult<OrderResponseDto>.Fail(500, "Failed to update order.");
        }

        return ServiceResult<OrderResponseDto>.Ok(OrderResponseDto.FromOrder(updatedOrder));
    }

    /// <summary>
    /// Compensating action: restores stock for items that were already decremented.
    /// </summary>
    private async Task RollbackStockAsync(List<(string ProductId, int Quantity)> decrementedItems)
    {
        foreach (var (productId, quantity) in decrementedItems)
        {
            try
            {
                await _productRepository.IncrementStockAsync(productId, quantity);
                _logger.LogInformation("Rolled back stock for product {ProductId}: +{Quantity}", productId, quantity);
            }
            catch (Exception ex)
            {
                // Log but don't throw — best-effort rollback
                _logger.LogError(ex, "Failed to rollback stock for product {ProductId}: +{Quantity}",
                    productId, quantity);
            }
        }
    }
}
