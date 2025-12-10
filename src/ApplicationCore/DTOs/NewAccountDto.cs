namespace ApplicationCore.DTOs;

public record NewAccountDto
{
    public string AccountName { get; init; } = string.Empty;
    public string AccountType { get; init; } = "Individual";
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
