using CommerceHub.Api.DTOs;
using CommerceHub.Api.Models;
using CommerceHub.Api.Repositories;
using CommerceHub.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace CommerceHub.Tests.Services;

[TestFixture]
public class ProductServiceTests
{
    private Mock<IProductRepository> _productRepoMock = null!;
    private Mock<ILogger<ProductService>> _loggerMock = null!;
    private ProductService _service = null!;

    [SetUp]
    public void Setup()
    {
        _productRepoMock = new Mock<IProductRepository>();
        _loggerMock = new Mock<ILogger<ProductService>>();
        _service = new ProductService(_productRepoMock.Object, _loggerMock.Object);
    }

    // -----------------------------------------------------------------------
    // Stock Adjustment — Positive (Add stock)
    // -----------------------------------------------------------------------

    [Test]
    public async Task AdjustStock_PositiveAdjustment_ShouldSucceed()
    {
        _productRepoMock.Setup(r => r.GetByIdAsync("prod-1"))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 10 });

        _productRepoMock.Setup(r => r.AdjustStockAsync("prod-1", 5))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 15 });

        var result = await _service.AdjustStockAsync("prod-1", new StockAdjustmentDto { Adjustment = 5 });

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Stock, Is.EqualTo(15));
    }

    // -----------------------------------------------------------------------
    // Stock Adjustment — Negative (Remove stock, valid)
    // -----------------------------------------------------------------------

    [Test]
    public async Task AdjustStock_NegativeAdjustment_WithSufficientStock_ShouldSucceed()
    {
        _productRepoMock.Setup(r => r.GetByIdAsync("prod-1"))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 10 });

        _productRepoMock.Setup(r => r.AdjustStockAsync("prod-1", -3))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 7 });

        var result = await _service.AdjustStockAsync("prod-1", new StockAdjustmentDto { Adjustment = -3 });

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Stock, Is.EqualTo(7));
    }

    // -----------------------------------------------------------------------
    // Stock Adjustment — Would Go Negative
    // -----------------------------------------------------------------------

    [Test]
    public async Task AdjustStock_WouldGoNegative_ShouldReturnConflict()
    {
        _productRepoMock.Setup(r => r.GetByIdAsync("prod-1"))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 3 });

        _productRepoMock.Setup(r => r.AdjustStockAsync("prod-1", -10))
            .ReturnsAsync((Product?)null);

        var result = await _service.AdjustStockAsync("prod-1", new StockAdjustmentDto { Adjustment = -10 });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(409));
        Assert.That(result.ErrorMessage, Does.Contain("negative stock"));
    }

    // -----------------------------------------------------------------------
    // Stock Adjustment — Product Not Found
    // -----------------------------------------------------------------------

    [Test]
    public async Task AdjustStock_ProductNotFound_ShouldReturn404()
    {
        _productRepoMock.Setup(r => r.GetByIdAsync("missing"))
            .ReturnsAsync((Product?)null);

        var result = await _service.AdjustStockAsync("missing", new StockAdjustmentDto { Adjustment = 5 });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
        Assert.That(result.ErrorMessage, Does.Contain("not found"));
    }

    // -----------------------------------------------------------------------
    // Stock Adjustment — Exact Stock Decrement to Zero
    // -----------------------------------------------------------------------

    [Test]
    public async Task AdjustStock_DecrementToExactlyZero_ShouldSucceed()
    {
        _productRepoMock.Setup(r => r.GetByIdAsync("prod-1"))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 5 });

        _productRepoMock.Setup(r => r.AdjustStockAsync("prod-1", -5))
            .ReturnsAsync(new Product { Id = "prod-1", Name = "Widget", Stock = 0 });

        var result = await _service.AdjustStockAsync("prod-1", new StockAdjustmentDto { Adjustment = -5 });

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Stock, Is.EqualTo(0));
    }
}
