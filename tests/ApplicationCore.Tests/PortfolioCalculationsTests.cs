using ApplicationCore.Domain;

namespace ApplicationCore.Tests;

public class PortfolioCalculationsTests
{
    [Fact]
    public void CalculateRealizedAndInvested_UsesAverageBuyCost()
    {
        var trades = new[]
        {
            new TradeCalculationRow(1, "BUY", 10m, 100m),
            new TradeCalculationRow(1, "BUY", 10m, 120m),
            new TradeCalculationRow(1, "SELL", 5m, 130m)
        };

        var result = PortfolioCalculations.CalculateRealizedAndInvested(trades);

        Assert.Equal(2200m, result.InvestedCapital);
        Assert.Equal(100m, result.RealizedPl);
    }

    [Fact]
    public void CalculateContributions_ClassifiesOnlyExternalCashFlows()
    {
        var entries = new[]
        {
            new CashContributionEntry("DEPOSIT", 1000m),
            new CashContributionEntry("CONTRIBUTION", 500m),
            new CashContributionEntry("WITHDRAWAL", -250m),
            new CashContributionEntry("TRANSFER_OUT", 100m),
            new CashContributionEntry("TRADE_BUY", -800m),
            new CashContributionEntry("TRADE_SELL", 200m),
            new CashContributionEntry("DIVIDEND", 25m),
            new CashContributionEntry("FEE", -10m),
            new CashContributionEntry("ADJUSTMENT", 5m)
        };

        var result = PortfolioCalculations.CalculateContributions(entries);

        Assert.Equal(1500m, result.Deposits);
        Assert.Equal(350m, result.Withdrawals);
    }

    [Fact]
    public void BuildReturnSeries_CalculatesFractionalReturns()
    {
        var prices = new[]
        {
            new SecurityPricePoint(new DateTime(2026, 1, 1), 100m),
            new SecurityPricePoint(new DateTime(2026, 1, 2), 110m),
            new SecurityPricePoint(new DateTime(2026, 1, 3), 99m)
        };

        var series = PortfolioCalculations.BuildReturnSeries(1, "MSFT", prices);

        Assert.Null(series[0].DailyReturnPct);
        Assert.Equal(0.10m, series[1].DailyReturnPct);
        Assert.Equal(-0.10m, series[2].DailyReturnPct);
        Assert.Equal(0.00m, series[2].CumReturnApprox);
    }

    [Fact]
    public void CalculateVolatility_ReturnsNullWithoutTwoReturns()
    {
        var result = PortfolioCalculations.CalculateVolatility(new decimal?[] { null, 0.01m });

        Assert.Null(result);
    }
}
