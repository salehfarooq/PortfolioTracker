using ApplicationCore.DTOs;
using ApplicationCore.Enums;

namespace ApplicationCore.Domain;

public static class PortfolioValidation
{
    public static void ValidateNewAccount(NewAccountDto account)
    {
        ArgumentNullException.ThrowIfNull(account);

        RequireText(account.Username, nameof(account.Username), 64);
        RequireText(account.FullName, nameof(account.FullName), 128);
        RequireText(account.Email, nameof(account.Email), 128);
        RequireText(account.AccountName, nameof(account.AccountName), 64);
        RequireText(account.AccountType, nameof(account.AccountType), 32);

        if (!account.Email.Contains('@') || account.Email.StartsWith("@", StringComparison.Ordinal))
        {
            throw new ArgumentException("Email must look like a valid email address.", nameof(account));
        }
    }

    public static void ValidateOrder(NewOrderDto order, decimal availableQuantity)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.AccountId <= 0)
        {
            throw new ArgumentException("Account id must be positive.", nameof(order));
        }

        if (order.SecurityId <= 0)
        {
            throw new ArgumentException("Security id must be positive.", nameof(order));
        }

        if (order.Quantity <= 0m)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(order));
        }

        if (order.Price <= 0m)
        {
            throw new ArgumentException("Price must be greater than zero.", nameof(order));
        }

        if (order.OrderType == OrderType.Sell && availableQuantity < order.Quantity)
        {
            throw new InvalidOperationException($"Insufficient quantity to sell. Available: {availableQuantity}, Requested: {order.Quantity}.");
        }
    }

    private static void RequireText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} must be {maxLength} characters or fewer.", fieldName);
        }
    }
}
