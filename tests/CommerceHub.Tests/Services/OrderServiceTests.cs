using CommerceHub.Api.DTOs;
using CommerceHub.Api.Messaging;
using CommerceHub.Api.Models;
using CommerceHub.Api.Repositories;
using CommerceHub.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace CommerceHub.Tests.Services;

[TestFixture]
public class OrderServiceTests
{
    private Mock<IOrderRepository> _orderRepoMock = null!;
    private Mock<IProductRepository> _productRepoMock = null!;
    private Mock<IEventPublisher> _eventPublisherMock = null!;
    private Mock<ILogger<OrderService>> _loggerMock = null!;
    private OrderService _service = null!;

    [SetUp]
    public void Setup()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _productRepoMock = new Mock<IProductRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _loggerMock = new Mock<ILogger<OrderService>>();

        _service = new OrderService(
            _orderRepoMock.Object,
            _productRepoMock.Object,
            _eventPublisherMock.Object,
            _loggerMock.Object
        );
    }

    // -----------------------------------------------------------------------
    // Checkout — Happy Path
    // -----------------------------------------------------------------------

    [Test]
    public async Task Checkout_WithSufficientStock_ShouldCreateOrderAndPublishEvent()
    {
        // Arrange
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-1",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 2 },
                new() { ProductId = "prod-2", Quantity = 1 }
            }
        };

        _productRepoMock.Setup(r => r.DecrementStockAsync("prod-1", 2))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Price = 10.00m, Stock = 8 });

        _productRepoMock.Setup(r => r.DecrementStockAsync("prod-2", 1))
            .ReturnsAsync(new Product { Id = "prod-2", Name = "Gadget", Price = 25.00m, Stock = 14 });

        _orderRepoMock.Setup(r => r.CreateAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => { o.Id = "order-123"; return o; });

        _eventPublisherMock.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OrderCreatedEvent>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CheckoutAsync(request);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.StatusCode, Is.EqualTo(201));
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data!.CustomerId, Is.EqualTo("cust-1"));
        Assert.That(result.Data.Items, Has.Count.EqualTo(2));
        Assert.That(result.Data.TotalAmount, Is.EqualTo(45.00m)); // (2*10) + (1*25)

        // Verify event was published exactly once
        _eventPublisherMock.Verify(
            p => p.PublishAsync("order.created", It.Is<OrderCreatedEvent>(e =>
                e.OrderId == "order-123" &&
                e.CustomerId == "cust-1" &&
                e.TotalAmount == 45.00m &&
                e.ItemCount == 2)),
            Times.Once);

        // Verify stock was decremented for both products
        _productRepoMock.Verify(r => r.DecrementStockAsync("prod-1", 2), Times.Once);
        _productRepoMock.Verify(r => r.DecrementStockAsync("prod-2", 1), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Checkout — Insufficient Stock
    // -----------------------------------------------------------------------

    [Test]
    public async Task Checkout_WithInsufficientStock_ShouldReturnConflictAndRollback()
    {
        // Arrange
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-1",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 2 },
                new() { ProductId = "prod-2", Quantity = 100 } // insufficient
            }
        };

        // First product succeeds
        _productRepoMock.Setup(r => r.DecrementStockAsync("prod-1", 2))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Price = 10m, Stock = 8 });

        // Second product fails (insufficient stock)
        _productRepoMock.Setup(r => r.DecrementStockAsync("prod-2", 100))
            .ReturnsAsync((Product?)null);

        // Product exists but has insufficient stock
        _productRepoMock.Setup(r => r.GetByIdAsync("prod-2"))
            .ReturnsAsync(new Product { Id = "prod-2", Name = "Gadget", Sku = "GAD-01", Stock = 15 });

        // Act
        var result = await _service.CheckoutAsync(request);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(409));
        Assert.That(result.ErrorMessage, Does.Contain("Insufficient stock"));

        // Verify rollback was called for the first product
        _productRepoMock.Verify(r => r.IncrementStockAsync("prod-1", 2), Times.Once);

        // Verify event was NOT published
        _eventPublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OrderCreatedEvent>()),
            Times.Never);

        // Verify order was NOT created
        _orderRepoMock.Verify(r => r.CreateAsync(It.IsAny<Order>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Checkout — Product Not Found
    // -----------------------------------------------------------------------

    [Test]
    public async Task Checkout_WithNonExistentProduct_ShouldReturnNotFound()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-1",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "non-existent", Quantity = 1 }
            }
        };

        _productRepoMock.Setup(r => r.DecrementStockAsync("non-existent", 1))
            .ReturnsAsync((Product?)null);

        _productRepoMock.Setup(r => r.GetByIdAsync("non-existent"))
            .ReturnsAsync((Product?)null);

        var result = await _service.CheckoutAsync(request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
        Assert.That(result.ErrorMessage, Does.Contain("not found"));

        // Event must NOT be published
        _eventPublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OrderCreatedEvent>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // Checkout — Event Not Published on Failure
    // -----------------------------------------------------------------------

    [Test]
    public async Task Checkout_OnFailure_ShouldNotPublishEvent()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-1",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 999 }
            }
        };

        _productRepoMock.Setup(r => r.DecrementStockAsync("prod-1", 999))
            .ReturnsAsync((Product?)null);
        _productRepoMock.Setup(r => r.GetByIdAsync("prod-1"))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Sku = "WID-01", Stock = 5 });

        await _service.CheckoutAsync(request);

        _eventPublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OrderCreatedEvent>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // GetById — Happy Path
    // -----------------------------------------------------------------------

    [Test]
    public async Task GetById_WhenOrderExists_ShouldReturnOrder()
    {
        var order = new Order
        {
            Id = "order-1",
            CustomerId = "cust-1",
            Status = OrderStatus.Pending,
            Items = new List<OrderItem>
            {
                new() { ProductId = "prod-1", Quantity = 2, UnitPrice = 10m }
            },
            TotalAmount = 20m
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync("order-1")).ReturnsAsync(order);

        var result = await _service.GetByIdAsync("order-1");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Id, Is.EqualTo("order-1"));
    }

    // -----------------------------------------------------------------------
    // GetById — Not Found
    // -----------------------------------------------------------------------

    [Test]
    public async Task GetById_WhenOrderNotFound_ShouldReturn404()
    {
        _orderRepoMock.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((Order?)null);

        var result = await _service.GetByIdAsync("missing");

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
    }

    // -----------------------------------------------------------------------
    // Update — Happy Path
    // -----------------------------------------------------------------------

    [Test]
    public async Task Update_WhenOrderIsPending_ShouldSucceed()
    {
        var existingOrder = new Order
        {
            Id = "order-1",
            CustomerId = "cust-1",
            Status = OrderStatus.Pending,
            Items = new List<OrderItem>(),
            TotalAmount = 0
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync("order-1")).ReturnsAsync(existingOrder);
        _orderRepoMock.Setup(r => r.UpdateAsync("order-1", It.IsAny<Order>()))
            .ReturnsAsync((string _, Order o) => o);

        var request = new UpdateOrderDto
        {
            CustomerId = "cust-1",
            Items = new List<UpdateOrderItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 3, UnitPrice = 15m }
            },
            Status = "Paid",
            TotalAmount = 45m
        };

        var result = await _service.UpdateAsync("order-1", request);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Status, Is.EqualTo("Paid"));
    }

    // -----------------------------------------------------------------------
    // Update — Blocked When Shipped
    // -----------------------------------------------------------------------

    [Test]
    public async Task Update_WhenOrderIsShipped_ShouldReturnConflict()
    {
        var shippedOrder = new Order
        {
            Id = "order-1",
            CustomerId = "cust-1",
            Status = OrderStatus.Shipped,
            Items = new List<OrderItem>(),
            TotalAmount = 50m
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync("order-1")).ReturnsAsync(shippedOrder);

        var request = new UpdateOrderDto
        {
            CustomerId = "cust-1",
            Items = new List<UpdateOrderItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 1, UnitPrice = 10m }
            },
            Status = "Cancelled",
            TotalAmount = 10m
        };

        var result = await _service.UpdateAsync("order-1", request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(409));
        Assert.That(result.ErrorMessage, Does.Contain("shipped"));
    }

    // -----------------------------------------------------------------------
    // Update — Order Not Found
    // -----------------------------------------------------------------------

    [Test]
    public async Task Update_WhenOrderNotFound_ShouldReturn404()
    {
        _orderRepoMock.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((Order?)null);

        var request = new UpdateOrderDto
        {
            CustomerId = "cust-1",
            Items = new List<UpdateOrderItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 1, UnitPrice = 10m }
            },
            Status = "Pending",
            TotalAmount = 10m
        };

        var result = await _service.UpdateAsync("missing", request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
    }

    // -----------------------------------------------------------------------
    // Update — Invalid Status
    // -----------------------------------------------------------------------

    [Test]
    public async Task Update_WithInvalidStatus_ShouldReturn400()
    {
        var existingOrder = new Order
        {
            Id = "order-1",
            CustomerId = "cust-1",
            Status = OrderStatus.Pending,
            Items = new List<OrderItem>(),
            TotalAmount = 0
        };

        _orderRepoMock.Setup(r => r.GetByIdAsync("order-1")).ReturnsAsync(existingOrder);

        var request = new UpdateOrderDto
        {
            CustomerId = "cust-1",
            Items = new List<UpdateOrderItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 1, UnitPrice = 10m }
            },
            Status = "InvalidStatus",
            TotalAmount = 10m
        };

        var result = await _service.UpdateAsync("order-1", request);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(400));
    }

    // -----------------------------------------------------------------------
    // Checkout — Single Item
    // -----------------------------------------------------------------------

    [Test]
    public async Task Checkout_SingleItem_ShouldCalculateCorrectTotal()
    {
        var request = new CheckoutRequestDto
        {
            CustomerId = "cust-1",
            Items = new List<CheckoutItemDto>
            {
                new() { ProductId = "prod-1", Quantity = 5 }
            }
        };

        _productRepoMock.Setup(r => r.DecrementStockAsync("prod-1", 5))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Price = 7.50m, Stock = 95 });

        _orderRepoMock.Setup(r => r.CreateAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order o) => { o.Id = "order-456"; return o; });

        _eventPublisherMock.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<OrderCreatedEvent>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CheckoutAsync(request);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.TotalAmount, Is.EqualTo(37.50m)); // 5 * 7.50
    }
}
