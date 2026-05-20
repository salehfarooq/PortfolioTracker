using ApplicationCore.Domain;
using ApplicationCore.DTOs;
using ApplicationCore.Enums;

namespace ApplicationCore.Tests;

public class PortfolioValidationTests
{
    [Fact]
    public void ValidateOrder_BlocksSellAboveAvailableQuantity()
    {
        var order = new NewOrderDto
        {
            AccountId = 1,
            SecurityId = 2,
            OrderType = OrderType.Sell,
            Quantity = 20m,
            Price = 10m
        };

        var ex = Assert.Throws<InvalidOperationException>(() => PortfolioValidation.ValidateOrder(order, 5m));

        Assert.Contains("Insufficient quantity", ex.Message);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void ValidateOrder_RejectsNonPositiveInputs(int accountId, int securityId, int quantity, int price)
    {
        var order = new NewOrderDto
        {
            AccountId = accountId,
            SecurityId = securityId,
            OrderType = OrderType.Buy,
            Quantity = quantity,
            Price = price
        };

        Assert.Throws<ArgumentException>(() => PortfolioValidation.ValidateOrder(order, 0m));
    }

    [Fact]
    public void ValidateNewAccount_RejectsInvalidEmail()
    {
        var account = new NewAccountDto
        {
            Username = "demo",
            FullName = "Demo User",
            Email = "not-an-email",
            AccountName = "Demo Portfolio",
            AccountType = "Individual"
        };

        Assert.Throws<ArgumentException>(() => PortfolioValidation.ValidateNewAccount(account));
    }
}

