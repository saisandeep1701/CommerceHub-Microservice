using CommerceHub.Api.DTOs;
using CommerceHub.Api.Models;

namespace CommerceHub.Api.Services;

/// <summary>
/// Represents the result of a service operation.
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public int StatusCode { get; init; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data, StatusCode = 200 };
    public static ServiceResult<T> Created(T data) => new() { Success = true, Data = data, StatusCode = 201 };
    public static ServiceResult<T> Fail(int statusCode, string message) => new() { Success = false, ErrorMessage = message, StatusCode = statusCode };
}

public interface IOrderService
{
    Task<ServiceResult<OrderResponseDto>> CheckoutAsync(CheckoutRequestDto request);
    Task<ServiceResult<OrderResponseDto>> GetByIdAsync(string id);
    Task<ServiceResult<OrderResponseDto>> UpdateAsync(string id, UpdateOrderDto request);
}
