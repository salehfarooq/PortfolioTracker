namespace ApplicationCore.DTOs;

public record TradeSummaryDto
{
    public int TradeId { get; init; }
    public int OrderId { get; init; }
    public int AccountId { get; init; }
    public int SecurityId { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string OrderType { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public DateTime TradeDate { get; init; }
}
