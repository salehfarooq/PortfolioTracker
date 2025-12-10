using ApplicationCore.Enums;

namespace ApplicationCore.DTOs;

public record NewOrderDto
{
    public int AccountId { get; init; }
    public int SecurityId { get; init; }
    public OrderType OrderType { get; init; }
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
}
