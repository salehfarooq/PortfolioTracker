namespace ApplicationCore.DTOs;

public record ReturnPointDto
{
    public int SecurityId { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public DateTime PriceDate { get; init; }
    public decimal ClosePrice { get; init; }
    public decimal? DailyReturnPct { get; init; }
    public decimal? CumReturnApprox { get; init; }
}
