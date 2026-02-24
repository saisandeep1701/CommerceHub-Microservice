using CommerceHub.Api.DTOs;
using CommerceHub.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CommerceHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<CheckoutRequestDto> _checkoutValidator;
    private readonly IValidator<UpdateOrderDto> _updateValidator;

    public OrdersController(
        IOrderService orderService,
        IValidator<CheckoutRequestDto> checkoutValidator,
        IValidator<UpdateOrderDto> updateValidator)
    {
        _orderService = orderService;
        _checkoutValidator = checkoutValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Process a new order. Validates input, verifies stock, decrements inventory
    /// atomically, creates the order, and publishes an OrderCreated event.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto request)
    {
        var validation = await _checkoutValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var result = await _orderService.CheckoutAsync(request);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Retrieve an order by its unique ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _orderService.GetByIdAsync(id);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Idempotent full replacement of order details.
    /// Blocked if order status is Shipped.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateOrderDto request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var result = await _orderService.UpdateAsync(id, request);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }
}
