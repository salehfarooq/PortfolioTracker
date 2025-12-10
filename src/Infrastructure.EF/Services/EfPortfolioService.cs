using ApplicationCore.DTOs;
using ApplicationCore.Enums;
using ApplicationCore.Services;
using Infrastructure.EF.Generated;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EF.Services;

public class EfPortfolioService : IPortfolioService
{
    private readonly PortfolioDbContext _context;

    public EfPortfolioService(PortfolioDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(int accountId)
    {
        try
        {
            var holdings = await LoadHoldingsAsync(accountId, null).ConfigureAwait(false);
            return holdings;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetHoldingsAsync({accountId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load holdings for account {accountId}.", ex);
        }
    }

    public async Task<PortfolioSnapshotDto> GetPortfolioSnapshotAsync(int accountId, DateTime? asOfDate)
    {
        try
        {
            var holdingsRaw = await _context.Holdings
                .AsNoTracking()
                .Include(h => h.Security)
                .Where(h => h.AccountID == accountId && h.Quantity != 0)
                .ToListAsync()
                .ConfigureAwait(false);

            var securityIds = holdingsRaw.Select(h => h.SecurityID).Distinct().ToList();
            var fallbackDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var effectiveDateOnly = asOfDate.HasValue
                ? DateOnly.FromDateTime(asOfDate.Value)
                : securityIds.Any()
                    ? await _context.PriceHistories
                        .Where(ph => securityIds.Contains(ph.SecurityID))
                        .MaxAsync(ph => (DateOnly?)ph.PriceDate)
                        .ConfigureAwait(false) ?? fallbackDate
                    : fallbackDate;

            var priceLookup = await GetPriceLookupAsync(securityIds, effectiveDateOnly).ConfigureAwait(false);
            var holdings = holdingsRaw
                .Select(h => MapHolding(h, priceLookup))
                .ToList();

            var totalMarketValue = holdings.Sum(h => h.MarketValue);
            var totalUnrealized = holdings.Sum(h => h.UnrealizedPL);

            var effectiveDateTime = effectiveDateOnly.ToDateTime(TimeOnly.MaxValue);
            var trades = await _context.Trades
                .AsNoTracking()
                .Where(t => t.Order.AccountID == accountId && t.TradeDate <= effectiveDateTime)
                .Select(t => new TradeRow(
                    t.Order.SecurityID,
                    t.Order.OrderType,
                    t.Quantity,
                    t.Price))
                .ToListAsync()
                .ConfigureAwait(false);

            var realizedAndInvested = CalculateRealizedAndInvested(trades);
            var totalReturnPct = realizedAndInvested.InvestedCapital > 0
                ? (totalMarketValue + realizedAndInvested.RealizedPl) / realizedAndInvested.InvestedCapital - 1
                : (decimal?)null;

            return new PortfolioSnapshotDto
            {
                AccountId = accountId,
                AsOfDate = effectiveDateOnly.ToDateTime(TimeOnly.MinValue),
                TotalMarketValue = totalMarketValue,
                TotalUnrealizedPL = totalUnrealized,
                TotalRealizedPL = realizedAndInvested.RealizedPl,
                TotalReturnPct = totalReturnPct,
                Holdings = holdings
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetPortfolioSnapshotAsync({accountId}, {asOfDate}) failed: {ex}");
            throw new InvalidOperationException($"Failed to build portfolio snapshot for account {accountId}.", ex);
        }
    }

    public async Task PlaceOrderAsync(NewOrderDto order)
    {
        try
        {
            var now = DateTime.UtcNow;
            var orderType = order.OrderType == OrderType.Buy ? "BUY" : "SELL";

            var existingQty = await _context.Holdings
                .AsNoTracking()
                .Where(h => h.AccountID == order.AccountId && h.SecurityID == order.SecurityId)
                .Select(h => h.Quantity)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (orderType == "SELL" && existingQty < order.Quantity)
            {
                throw new InvalidOperationException($"Insufficient quantity to sell. Available: {existingQty}, Requested: {order.Quantity}.");
            }

            var newOrder = new Order
            {
                AccountID = order.AccountId,
                SecurityID = order.SecurityId,
                OrderType = orderType,
                Quantity = order.Quantity,
                Price = order.Price,
                Status = "Filled",
                OrderDate = now
            };

            await _context.Orders.AddAsync(newOrder).ConfigureAwait(false);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            var trade = new Trade
            {
                OrderID = newOrder.OrderID,
                TradeDate = now,
                Quantity = order.Quantity,
                Price = order.Price,
                Amount = order.Quantity * order.Price,
                Fees = 0m
            };

            await _context.Trades.AddAsync(trade).ConfigureAwait(false);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.PlaceOrderAsync failed for account {order.AccountId}, security {order.SecurityId}: {ex}");
            throw new InvalidOperationException("Failed to place order.", ex);
        }
    }

    public async Task<IReadOnlyList<ReturnPointDto>> GetSecurityReturnSeriesAsync(int securityId, DateTime? start, DateTime? end)
    {
        try
        {
            DateOnly? startDate = start.HasValue ? DateOnly.FromDateTime(start.Value) : null;
            DateOnly? endDate = end.HasValue ? DateOnly.FromDateTime(end.Value) : null;

            var ticker = await _context.Securities
                .AsNoTracking()
                .Where(s => s.SecurityID == securityId)
                .Select(s => s.Ticker)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false) ?? string.Empty;

            var prices = await _context.PriceHistories
                .AsNoTracking()
                .Where(ph => ph.SecurityID == securityId)
                .Where(ph => !startDate.HasValue || ph.PriceDate >= startDate.Value)
                .Where(ph => !endDate.HasValue || ph.PriceDate <= endDate.Value)
                .OrderBy(ph => ph.PriceDate)
                .ToListAsync()
                .ConfigureAwait(false);

            decimal? previousClose = null;
            decimal? cumulativeReturn = null;
            var points = new List<ReturnPointDto>(prices.Count);

            foreach (var price in prices)
            {
                decimal? dailyReturn = null;
                if (previousClose.HasValue && previousClose.Value != 0)
                {
                    dailyReturn = (price.ClosePrice - previousClose.Value) / previousClose.Value;
                    cumulativeReturn = (cumulativeReturn ?? 0) + dailyReturn;
                }

                points.Add(new ReturnPointDto
                {
                    SecurityId = price.SecurityID,
                    Ticker = ticker,
                    PriceDate = price.PriceDate.ToDateTime(TimeOnly.MinValue),
                    ClosePrice = price.ClosePrice,
                    DailyReturnPct = dailyReturn,
                    CumReturnApprox = cumulativeReturn
                });

                previousClose = price.ClosePrice;
            }

            return points;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetSecurityReturnSeriesAsync({securityId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load return series for security {securityId}.", ex);
        }
    }

    public async Task<IReadOnlyList<TradeSummaryDto>> GetRecentTradesAsync(int accountId, int take)
    {
        try
        {
            return await _context.Trades
                .AsNoTracking()
                .Where(t => t.Order.AccountID == accountId)
                .OrderByDescending(t => t.TradeDate)
                .Take(take)
                .Select(t => new TradeSummaryDto
                {
                    TradeId = t.TradeID,
                    OrderId = t.OrderID,
                    AccountId = t.Order.AccountID,
                    SecurityId = t.Order.SecurityID,
                    Ticker = t.Order.Security.Ticker,
                    OrderType = t.Order.OrderType,
                    Quantity = t.Quantity,
                    Price = t.Price,
                    TradeDate = t.TradeDate
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetRecentTradesAsync({accountId}, {take}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load recent trades for account {accountId}.", ex);
        }
    }

    public async Task<IReadOnlyList<CashLedgerEntryDto>> GetRecentCashActivityAsync(int accountId, int take)
    {
        try
        {
            return await _context.CashLedgers
                .AsNoTracking()
                .Where(c => c.AccountID == accountId)
                .OrderByDescending(c => c.TxnDate)
                .Take(take)
                .Select(c => new CashLedgerEntryDto
                {
                    LedgerId = c.LedgerID,
                    AccountId = c.AccountID,
                    TxnDate = c.TxnDate,
                    Amount = c.Amount,
                    Type = c.Type,
                    Reference = c.Reference ?? string.Empty
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetRecentCashActivityAsync({accountId}, {take}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load cash activity for account {accountId}.", ex);
        }
    }

    public async Task<IReadOnlyList<HoldingDto>> GetTopAssetsAsync(int accountId, int topN, string metric)
    {
        try
        {
            var holdings = await LoadHoldingsAsync(accountId, null).ConfigureAwait(false);
            if (holdings.Count == 0)
            {
                return holdings;
            }

            var metricKey = metric?.Trim()?.ToLowerInvariant() ?? string.Empty;
            IEnumerable<HoldingDto> ordered = metricKey switch
            {
                "unrealizedpl" => holdings.OrderByDescending(h => h.UnrealizedPL),
                "returnpct" => holdings.OrderByDescending(h =>
                {
                    var cost = h.Quantity * h.AvgCost;
                    return cost > 0 ? h.UnrealizedPL / cost : decimal.MinValue;
                }),
                _ => holdings.OrderByDescending(h => h.MarketValue)
            };

            return ordered.Take(topN).ToList();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetTopAssetsAsync({accountId}, {topN}, {metric}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load top assets for account {accountId}.", ex);
        }
    }

    public async Task<decimal?> GetVolatilityAsync(int securityId, DateTime? start, DateTime? end)
    {
        try
        {
            DateOnly? startDate = start.HasValue ? DateOnly.FromDateTime(start.Value) : null;
            DateOnly? endDate = end.HasValue ? DateOnly.FromDateTime(end.Value) : null;

            var prices = await _context.PriceHistories
                .AsNoTracking()
                .Where(ph => ph.SecurityID == securityId)
                .Where(ph => !startDate.HasValue || ph.PriceDate >= startDate.Value)
                .Where(ph => !endDate.HasValue || ph.PriceDate <= endDate.Value)
                .OrderBy(ph => ph.PriceDate)
                .ToListAsync()
                .ConfigureAwait(false);

            var returns = new List<decimal>();
            decimal? previousClose = null;

            foreach (var ph in prices)
            {
                if (previousClose.HasValue && previousClose.Value != 0)
                {
                    var daily = (ph.ClosePrice - previousClose.Value) / previousClose.Value;
                    returns.Add(daily);
                }

                previousClose = ph.ClosePrice;
            }

            if (returns.Count < 2)
            {
                return null;
            }

            var mean = returns.Average();
            var variance = returns.Select(r => Math.Pow((double)(r - mean), 2)).Average();
            var stdDev = Math.Sqrt(variance);
            return (decimal)stdDev;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetVolatilityAsync({securityId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to calculate volatility for security {securityId}.", ex);
        }
    }

    public async Task<IReadOnlyList<SecurityDto>> GetSecuritiesAsync(bool activeOnly)
    {
        try
        {
            var query = _context.Securities.AsNoTracking();
            if (activeOnly)
            {
                query = query.Where(s => s.IsActive);
            }

            return await query
                .OrderBy(s => s.Ticker)
                .Select(s => new SecurityDto
                {
                    SecurityId = s.SecurityID,
                    Ticker = s.Ticker,
                    CompanyName = s.CompanyName,
                    Sector = s.Sector ?? string.Empty,
                    ListedIn = s.ListedIn ?? string.Empty,
                    IsActive = s.IsActive
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EfPortfolioService.GetSecuritiesAsync({activeOnly}) failed: {ex}");
            throw new InvalidOperationException("Failed to load securities.", ex);
        }
    }

    private async Task<List<HoldingDto>> LoadHoldingsAsync(int accountId, DateOnly? asOfDate)
    {
        var holdingsRaw = await _context.Holdings
            .AsNoTracking()
            .Include(h => h.Security)
            .Where(h => h.AccountID == accountId && h.Quantity != 0)
            .ToListAsync()
            .ConfigureAwait(false);

        var securityIds = holdingsRaw.Select(h => h.SecurityID).Distinct().ToList();
        var priceLookup = await GetPriceLookupAsync(securityIds, asOfDate).ConfigureAwait(false);
        return holdingsRaw
            .Select(h => MapHolding(h, priceLookup))
            .ToList();
    }

    private async Task<Dictionary<int, (decimal ClosePrice, DateOnly PriceDate)>> GetPriceLookupAsync(IEnumerable<int> securityIds, DateOnly? asOfDate)
    {
        var ids = securityIds.Distinct().ToList();
        if (!ids.Any())
        {
            return new Dictionary<int, (decimal, DateOnly)>();
        }

        IQueryable<PriceHistory> query = _context.PriceHistories.AsNoTracking().Where(ph => ids.Contains(ph.SecurityID));
        if (asOfDate.HasValue)
        {
            query = query.Where(ph => ph.PriceDate <= asOfDate.Value);
        }

        var latestPrices = await query
            .GroupBy(ph => ph.SecurityID)
            .Select(g => g.OrderByDescending(ph => ph.PriceDate).First())
            .ToListAsync()
            .ConfigureAwait(false);

        return latestPrices.ToDictionary(p => p.SecurityID, p => (p.ClosePrice, p.PriceDate));
    }

    private static (decimal RealizedPl, decimal InvestedCapital) CalculateRealizedAndInvested(IEnumerable<TradeRow> trades)
    {
        decimal realized = 0m;
        decimal invested = 0m;

        var grouped = trades.GroupBy(t => t.SecurityId);
        foreach (var group in grouped)
        {
            var buys = group.Where(t => string.Equals(t.OrderType, "BUY", StringComparison.OrdinalIgnoreCase)).ToList();
            var sells = group.Where(t => string.Equals(t.OrderType, "SELL", StringComparison.OrdinalIgnoreCase)).ToList();

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

    private static HoldingDto MapHolding(Holding holding, Dictionary<int, (decimal ClosePrice, DateOnly PriceDate)> priceLookup)
    {
        var price = priceLookup.TryGetValue(holding.SecurityID, out var p)
            ? p.ClosePrice
            : 0m;

        var marketValue = price * holding.Quantity;
        var unrealized = (price - holding.AvgCost) * holding.Quantity;

        return new HoldingDto
        {
            AccountId = holding.AccountID,
            SecurityId = holding.SecurityID,
            Ticker = holding.Security.Ticker,
            CompanyName = holding.Security.CompanyName,
            Sector = holding.Security.Sector ?? string.Empty,
            Quantity = holding.Quantity,
            AvgCost = holding.AvgCost,
            LatestPrice = price,
            MarketValue = marketValue,
            UnrealizedPL = unrealized
        };
    }

    private sealed record TradeRow(int SecurityId, string OrderType, decimal Quantity, decimal Price);
}
