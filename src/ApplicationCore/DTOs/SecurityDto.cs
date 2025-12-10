namespace ApplicationCore.DTOs;

public record SecurityDto
{
    public int SecurityId { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string Sector { get; init; } = string.Empty;
    public string ListedIn { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
