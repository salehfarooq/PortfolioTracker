namespace ApplicationCore.DTOs;

public record AccountSummaryDto
{
    public int AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
}
