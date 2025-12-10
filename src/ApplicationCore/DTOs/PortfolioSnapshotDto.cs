using System.Collections.Generic;

namespace ApplicationCore.DTOs;

public record PortfolioSnapshotDto
{
    public int AccountId { get; init; }
    public DateTime AsOfDate { get; init; }
    public decimal TotalMarketValue { get; init; }
    public decimal TotalUnrealizedPL { get; init; }
    public decimal? TotalRealizedPL { get; init; }
    public decimal? TotalReturnPct { get; init; }
    public IReadOnlyList<HoldingDto> Holdings { get; init; } = Array.Empty<HoldingDto>();
}
