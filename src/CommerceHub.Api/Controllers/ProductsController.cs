using CommerceHub.Api.DTOs;
using CommerceHub.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CommerceHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IValidator<StockAdjustmentDto> _stockValidator;

    public ProductsController(
        IProductService productService,
        IValidator<StockAdjustmentDto> stockValidator)
    {
        _productService = productService;
        _stockValidator = stockValidator;
    }

    /// <summary>
    /// Direct inventory adjustment. Atomic operation that prevents negative stock levels.
    /// </summary>
    [HttpPatch("{id}/stock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustStock(string id, [FromBody] StockAdjustmentDto request)
    {
        var validation = await _stockValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var result = await _productService.AdjustStockAsync(id, request);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            productId = result.Data!.Id,
            name = result.Data.Name,
            stock = result.Data.Stock,
            updatedAt = result.Data.UpdatedAt
        });
    }
}
