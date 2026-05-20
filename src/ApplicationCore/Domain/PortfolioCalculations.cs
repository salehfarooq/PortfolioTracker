using ApplicationCore.DTOs;

namespace ApplicationCore.Domain;

public static class PortfolioCalculations
{
    public static (decimal RealizedPl, decimal InvestedCapital) CalculateRealizedAndInvested(IEnumerable<TradeCalculationRow> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);

        decimal realized = 0m;
        decimal invested = 0m;

        foreach (var group in trades.GroupBy(t => t.SecurityId))
        {
            var buys = group.Where(IsBuy).ToList();
            var sells = group.Where(IsSell).ToList();

            var totalBuyQty = buys.Sum(b => b.Quantity);
            var totalBuyCost = buys.Sum(b => b.Quantity * b.Price);
            invested += totalBuyCost;

            var avgCost = totalBuyQty > 0 ? totalBuyCost / totalBuyQty : 0m;
            var sellQty = sells.Sum(s => s.Quantity);
            var sellProceeds = sells.Sum(s => s.Quantity * s.Price);
            realized += sellProceeds - (sellQty * avgCost);
        }

        return (realized, invested);
    }

    public static decimal CalculateRealized(IEnumerable<TradeCalculationRow> trades)
    {
        return CalculateRealizedAndInvested(trades).RealizedPl;
    }

    public static (decimal Deposits, decimal Withdrawals) CalculateContributions(IEnumerable<CashContributionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        decimal deposits = 0m;
        decimal withdrawals = 0m;

        foreach (var entry in entries)
        {
            var type = (entry.Type ?? string.Empty).Trim().ToLowerInvariant();
            var amount = entry.Amount;

            if (IsExternalCashIn(type))
            {
                deposits += Math.Abs(amount);
            }
            else if (IsExternalCashOut(type))
            {
                withdrawals += Math.Abs(amount);
            }
        }

        return (deposits, withdrawals);
    }

    public static IReadOnlyList<ReturnPointDto> BuildReturnSeries(
        int securityId,
        string ticker,
        IEnumerable<SecurityPricePoint> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        decimal? previousClose = null;
        decimal cumulativeReturn = 0m;
        var points = new List<ReturnPointDto>();

        foreach (var price in prices.OrderBy(p => p.PriceDate))
        {
            decimal? dailyReturn = null;
            if (previousClose.HasValue && previousClose.Value != 0m)
            {
                dailyReturn = (price.ClosePrice - previousClose.Value) / previousClose.Value;
                cumulativeReturn += dailyReturn.Value;
            }

            points.Add(new ReturnPointDto
            {
                SecurityId = securityId,
                Ticker = ticker,
                PriceDate = price.PriceDate,
                ClosePrice = price.ClosePrice,
                DailyReturnPct = dailyReturn,
                CumReturnApprox = points.Count == 0 ? null : cumulativeReturn
            });

            previousClose = price.ClosePrice;
        }

        return points;
    }

    public static decimal? CalculateVolatility(IEnumerable<decimal?> returns)
    {
        ArgumentNullException.ThrowIfNull(returns);

        var values = returns
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (values.Count < 2)
        {
            return null;
        }

        var mean = values.Average();
        var variance = values.Select(r => Math.Pow((double)(r - mean), 2)).Average();
        return (decimal)Math.Sqrt(variance);
    }

    private static bool IsBuy(TradeCalculationRow row)
    {
        return string.Equals(row.OrderType, "BUY", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSell(TradeCalculationRow row)
    {
        return string.Equals(row.OrderType, "SELL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExternalCashIn(string type)
    {
        return type.StartsWith("deposit", StringComparison.Ordinal) ||
               type.StartsWith("contribution", StringComparison.Ordinal) ||
               type.StartsWith("cash_in", StringComparison.Ordinal) ||
               type.StartsWith("transfer_in", StringComparison.Ordinal);
    }

    private static bool IsExternalCashOut(string type)
    {
        return type.StartsWith("withdraw", StringComparison.Ordinal) ||
               type.StartsWith("cash_out", StringComparison.Ordinal) ||
               type.StartsWith("transfer_out", StringComparison.Ordinal);
    }
}
