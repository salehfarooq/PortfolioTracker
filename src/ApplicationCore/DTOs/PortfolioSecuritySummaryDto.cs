namespace ApplicationCore.DTOs;

public record PortfolioSecuritySummaryDto
{
    public int SecurityId { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal AvgCost { get; init; }
    public decimal LatestPrice { get; init; }
    public decimal MarketValue { get; init; }
    public decimal UnrealizedPL { get; init; }
}
