namespace ApplicationCore.DTOs;

public record CashLedgerEntryDto
{
    public int LedgerId { get; init; }
    public int AccountId { get; init; }
    public DateTime TxnDate { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
}
