namespace ApplicationCore.DTOs;

public record UserSummaryDto
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public int AccountCount { get; init; }
    public int ActiveAccountCount { get; init; }
}
