namespace ApplicationCore.DTOs;

public record PortfolioOverviewDto
{
    public int? AccountId { get; init; }
    public int? UserId { get; init; }
    public IReadOnlyList<PortfolioSecuritySummaryDto> Securities { get; init; } = Array.Empty<PortfolioSecuritySummaryDto>();
    public decimal TotalSecurityValue { get; init; }
    public decimal CashBalance { get; init; }
    public decimal TotalPortfolioValue { get; init; }
    public decimal TotalUnrealizedPL { get; init; }
    public decimal TotalRealizedPL { get; init; }
    public decimal NetContribution { get; init; }
    public decimal? TotalReturnPct { get; init; }
}
