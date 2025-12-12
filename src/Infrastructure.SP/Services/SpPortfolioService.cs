using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ApplicationCore.DataAccess;
using ApplicationCore.DTOs;
using ApplicationCore.Enums;
using ApplicationCore.Services;
using Microsoft.Data.SqlClient;

namespace Infrastructure.SP.Services;

public class SpPortfolioService : IPortfolioService
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SpPortfolioService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(int accountId)
    {
        const string sql = @"
SELECT hv.AccountID, hv.SecurityID, hv.Ticker, hv.CompanyName, s.Sector, hv.Quantity, hv.AvgCost, hv.LatestClosePrice, hv.MarketValue, hv.UnrealizedPL
FROM v_AccountHoldingsValue hv
INNER JOIN Securities s ON hv.SecurityID = s.SecurityID
WHERE hv.AccountID = @AccountID AND hv.Quantity <> 0;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            cmd.Parameters.AddWithValue("@AccountID", accountId);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var holdings = new List<HoldingDto>();

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                holdings.Add(new HoldingDto
                {
                    AccountId = reader.GetInt32(0),
                    SecurityId = reader.GetInt32(1),
                    Ticker = reader.GetString(2),
                    CompanyName = reader.GetString(3),
                    Sector = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Quantity = reader.GetDecimal(5),
                    AvgCost = reader.GetDecimal(6),
                    LatestPrice = reader.GetDecimal(7),
                    MarketValue = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                    UnrealizedPL = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9)
                });
            }

            return holdings;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetHoldingsAsync({accountId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load holdings for account {accountId} via stored procedures.", ex);
        }
    }

    public async Task<PortfolioSnapshotDto> GetPortfolioSnapshotAsync(int accountId, DateTime? asOfDate)
    {
        const string snapshotProc = "usp_GetPortfolioSnapshot";
        var holdings = new List<HoldingDto>();
        DateTime effectiveAsOf = asOfDate ?? DateTime.UtcNow;

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using (var cmd = new SqlCommand(snapshotProc, (SqlConnection)conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@AccountID", accountId);
                var asOfParam = cmd.Parameters.Add("@AsOfDate", SqlDbType.DateTime);
                asOfParam.Value = (object?)asOfDate ?? DBNull.Value;

                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                var hasAsOf = TryGetOrdinal(reader, "AsOfDate", out var asOfOrdinal);
                var hasSector = TryGetOrdinal(reader, "Sector", out var sectorOrdinal);
                var hasMarketValue = TryGetOrdinal(reader, "MarketValue", out var marketValueOrdinal);
                var hasUnrealized = TryGetOrdinal(reader, "UnrealizedPL", out var unrealizedOrdinal);
                var hasLatestPrice = TryGetOrdinal(reader, "LatestPrice", out var latestPriceOrdinal);
                var rowIndex = 0;

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    if (rowIndex == 0 && hasAsOf && !reader.IsDBNull(asOfOrdinal))
                    {
                        effectiveAsOf = reader.GetDateTime(asOfOrdinal);
                    }

                    holdings.Add(new HoldingDto
                    {
                        AccountId = reader.GetInt32(reader.GetOrdinal("AccountID")),
                        SecurityId = reader.GetInt32(reader.GetOrdinal("SecurityID")),
                        Ticker = reader.GetString(reader.GetOrdinal("Ticker")),
                        CompanyName = reader.GetString(reader.GetOrdinal("CompanyName")),
                        Sector = hasSector ? SafeGetString(reader, sectorOrdinal) : string.Empty,
                        Quantity = reader.GetDecimal(reader.GetOrdinal("Quantity")),
                        AvgCost = reader.GetDecimal(reader.GetOrdinal("AvgCost")),
                        LatestPrice = hasLatestPrice ? SafeGetDecimal(reader, latestPriceOrdinal) : 0m,
                        MarketValue = hasMarketValue ? SafeGetDecimal(reader, marketValueOrdinal) : reader.GetDecimal(reader.GetOrdinal("Quantity")) * (hasLatestPrice ? SafeGetDecimal(reader, latestPriceOrdinal) : reader.GetDecimal(reader.GetOrdinal("AvgCost"))),
                        UnrealizedPL = hasUnrealized ? SafeGetDecimal(reader, unrealizedOrdinal) : 0m
                    });

                    rowIndex++;
                }
            }

            var totalMarket = holdings.Sum(h => h.MarketValue);
            var totalUnrealized = holdings.Sum(h => h.UnrealizedPL);

            var (realized, invested) = await CalculateRealizedAndInvestedAsync(accountId, asOfDate).ConfigureAwait(false);
            var totalReturnPct = invested > 0 ? (totalMarket + realized) / invested - 1 : (decimal?)null;

            return new PortfolioSnapshotDto
            {
                AccountId = accountId,
                AsOfDate = effectiveAsOf,
                Holdings = holdings,
                TotalMarketValue = totalMarket,
                TotalUnrealizedPL = totalUnrealized,
                TotalRealizedPL = realized,
                TotalReturnPct = totalReturnPct
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetPortfolioSnapshotAsync({accountId}, {asOfDate}) failed: {ex}");
            throw new InvalidOperationException($"Failed to build portfolio snapshot for account {accountId} via stored procedures.", ex);
        }
    }

    public async Task PlaceOrderAsync(NewOrderDto order)
    {
        const string insertOrderSql = @"
INSERT INTO Orders (AccountID, SecurityID, OrderType, Quantity, Price, Status, OrderDate)
OUTPUT INSERTED.OrderID
VALUES (@AccountID, @SecurityID, @OrderType, @Quantity, @Price, 'Filled', SYSUTCDATETIME());";

        const string insertTradeSql = @"
INSERT INTO Trades (OrderID, TradeDate, Quantity, Price, Amount, Fees)
VALUES (@OrderID, SYSUTCDATETIME(), @Quantity, @Price, @Amount, @Fees);";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            var sqlConn = (SqlConnection)conn;

            var existingQty = await GetHoldingQuantityAsync(sqlConn, order.AccountId, order.SecurityId).ConfigureAwait(false);
            if (order.OrderType == OrderType.Sell && existingQty < order.Quantity)
            {
                throw new InvalidOperationException($"Insufficient quantity to sell. Available: {existingQty}, Requested: {order.Quantity}.");
            }

            using var tx = (SqlTransaction)await sqlConn.BeginTransactionAsync().ConfigureAwait(false);

            int orderId;
            using (var cmd = new SqlCommand(insertOrderSql, sqlConn, tx))
            {
                cmd.Parameters.AddWithValue("@AccountID", order.AccountId);
                cmd.Parameters.AddWithValue("@SecurityID", order.SecurityId);
                cmd.Parameters.AddWithValue("@OrderType", order.OrderType == OrderType.Buy ? "BUY" : "SELL");
                cmd.Parameters.AddWithValue("@Quantity", order.Quantity);
                cmd.Parameters.AddWithValue("@Price", order.Price);

                var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                orderId = Convert.ToInt32(result);
            }

            using (var tradeCmd = new SqlCommand(insertTradeSql, sqlConn, tx))
            {
                tradeCmd.Parameters.AddWithValue("@OrderID", orderId);
                tradeCmd.Parameters.AddWithValue("@Quantity", order.Quantity);
                tradeCmd.Parameters.AddWithValue("@Price", order.Price);
                tradeCmd.Parameters.AddWithValue("@Amount", order.Quantity * order.Price);
                tradeCmd.Parameters.AddWithValue("@Fees", 0m);

                await tradeCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.PlaceOrderAsync failed for account {order.AccountId}, security {order.SecurityId}: {ex}");
            throw new InvalidOperationException("Failed to place order via stored procedures.", ex);
        }
    }

    public async Task<IReadOnlyList<ReturnPointDto>> GetSecurityReturnSeriesAsync(int securityId, DateTime? start, DateTime? end)
    {
        const string procName = "usp_GetSecurityReturnSeries";
        var points = new List<ReturnPointDto>();

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(procName, (SqlConnection)conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@SecurityID", securityId);
            var startParam = cmd.Parameters.Add("@StartDate", SqlDbType.DateTime);
            startParam.Value = (object?)start ?? DBNull.Value;
            var endParam = cmd.Parameters.Add("@EndDate", SqlDbType.DateTime);
            endParam.Value = (object?)end ?? DBNull.Value;

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            decimal? previousClose = null;
            decimal? cumulative = null;
            string ticker = string.Empty;

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var priceDate = reader.GetDateTime(reader.GetOrdinal("PriceDate"));
                var closePrice = reader.GetDecimal(reader.GetOrdinal("ClosePrice"));
                ticker = SafeGetString(reader, "Ticker", ticker);

                decimal? dailyReturn = null;
                if (previousClose.HasValue && previousClose.Value != 0)
                {
                    dailyReturn = (closePrice - previousClose.Value) / previousClose.Value;
                    cumulative = (cumulative ?? 0) + dailyReturn;
                }

                points.Add(new ReturnPointDto
                {
                    SecurityId = securityId,
                    Ticker = ticker,
                    PriceDate = priceDate,
                    ClosePrice = closePrice,
                    DailyReturnPct = SafeGetDecimal(reader, "DailyReturnPct", dailyReturn),
                    CumReturnApprox = cumulative
                });

                previousClose = closePrice;
            }

            return points;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetSecurityReturnSeriesAsync({securityId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load return series for security {securityId} via stored procedures.", ex);
        }
    }

    public async Task<IReadOnlyList<TradeSummaryDto>> GetRecentTradesAsync(int accountId, int take)
    {
        const string sql = @"
SELECT TOP (@Take)
    t.TradeID,
    t.OrderID,
    o.AccountID,
    o.SecurityID,
    s.Ticker,
    o.OrderType,
    t.Quantity,
    t.Price,
    t.TradeDate
FROM Trades t
INNER JOIN Orders o ON t.OrderID = o.OrderID
INNER JOIN Securities s ON o.SecurityID = s.SecurityID
WHERE o.AccountID = @AccountID
ORDER BY t.TradeDate DESC;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            cmd.Parameters.AddWithValue("@AccountID", accountId);
            cmd.Parameters.AddWithValue("@Take", take);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var trades = new List<TradeSummaryDto>();

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                trades.Add(new TradeSummaryDto
                {
                    TradeId = reader.GetInt32(0),
                    OrderId = reader.GetInt32(1),
                    AccountId = reader.GetInt32(2),
                    SecurityId = reader.GetInt32(3),
                    Ticker = reader.GetString(4),
                    OrderType = reader.GetString(5),
                    Quantity = reader.GetDecimal(6),
                    Price = reader.GetDecimal(7),
                    TradeDate = reader.GetDateTime(8)
                });
            }

            return trades;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetRecentTradesAsync({accountId}, {take}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load recent trades for account {accountId} via stored procedures.", ex);
        }
    }

    public async Task<IReadOnlyList<CashLedgerEntryDto>> GetRecentCashActivityAsync(int accountId, int take)
    {
        const string sql = @"
SELECT TOP (@Take) LedgerID, AccountID, TxnDate, Amount, Type, Reference
FROM CashLedger
WHERE AccountID = @AccountID
ORDER BY TxnDate DESC;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            cmd.Parameters.AddWithValue("@AccountID", accountId);
            cmd.Parameters.AddWithValue("@Take", take);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var results = new List<CashLedgerEntryDto>();

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(new CashLedgerEntryDto
                {
                    LedgerId = reader.GetInt32(0),
                    AccountId = reader.GetInt32(1),
                    TxnDate = reader.GetDateTime(2),
                    Amount = reader.GetDecimal(3),
                    Type = reader.GetString(4),
                    Reference = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetRecentCashActivityAsync({accountId}, {take}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load cash activity for account {accountId} via stored procedures.", ex);
        }
    }

    public async Task<IReadOnlyList<HoldingDto>> GetTopAssetsAsync(int accountId, int topN, string metric)
    {
        var metricKey = metric?.Trim()?.ToLowerInvariant() ?? string.Empty;
        var orderClause = metricKey switch
        {
            "unrealizedpl" => "ORDER BY hv.UnrealizedPL DESC",
            "returnpct" => "ORDER BY CASE WHEN hv.AvgCost * hv.Quantity > 0 THEN hv.UnrealizedPL / (hv.AvgCost * hv.Quantity) ELSE -1 END DESC",
            _ => "ORDER BY hv.MarketValue DESC"
        };

        var sql = $@"
SELECT TOP (@TopN) hv.AccountID, hv.SecurityID, hv.Ticker, hv.CompanyName, s.Sector, hv.Quantity, hv.AvgCost, hv.LatestClosePrice, hv.MarketValue, hv.UnrealizedPL
FROM v_AccountHoldingsValue hv
INNER JOIN Securities s ON hv.SecurityID = s.SecurityID
WHERE hv.AccountID = @AccountID AND hv.Quantity <> 0
{orderClause};";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            cmd.Parameters.AddWithValue("@AccountID", accountId);
            cmd.Parameters.AddWithValue("@TopN", topN);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var holdings = new List<HoldingDto>();

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                holdings.Add(new HoldingDto
                {
                    AccountId = reader.GetInt32(0),
                    SecurityId = reader.GetInt32(1),
                    Ticker = reader.GetString(2),
                    CompanyName = reader.GetString(3),
                    Sector = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Quantity = reader.GetDecimal(5),
                    AvgCost = reader.GetDecimal(6),
                    LatestPrice = reader.GetDecimal(7),
                    MarketValue = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                    UnrealizedPL = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9)
                });
            }

            return holdings;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetTopAssetsAsync({accountId}, {topN}, {metric}) failed: {ex}");
            throw new InvalidOperationException($"Failed to load top assets for account {accountId} via stored procedures.", ex);
        }
    }

    public async Task<decimal?> GetVolatilityAsync(int securityId, DateTime? start, DateTime? end)
    {
        try
        {
            var series = await GetSecurityReturnSeriesAsync(securityId, start, end).ConfigureAwait(false);
            var returns = series
                .Where(p => p.DailyReturnPct.HasValue)
                .Select(p => p.DailyReturnPct!.Value)
                .ToList();

            if (returns.Count < 2)
            {
                return null;
            }

            var mean = returns.Average();
            var variance = returns.Select(r => Math.Pow((double)(r - mean), 2)).Average();
            return (decimal)Math.Sqrt(variance);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetVolatilityAsync({securityId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to calculate volatility for security {securityId} via stored procedures.", ex);
        }
    }

    public async Task<IReadOnlyList<SecurityDto>> GetSecuritiesAsync(bool activeOnly)
    {
        var sql = @"
SELECT SecurityID, Ticker, CompanyName, Sector, ListedIn, IsActive
FROM Securities";
        if (activeOnly)
        {
            sql += " WHERE IsActive = 1";
        }
        sql += " ORDER BY Ticker;";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();
            using var cmd = new SqlCommand(sql, (SqlConnection)conn);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var list = new List<SecurityDto>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new SecurityDto
                {
                    SecurityId = reader.GetInt32(0),
                    Ticker = reader.GetString(1),
                    CompanyName = reader.GetString(2),
                    Sector = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ListedIn = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    IsActive = reader.GetBoolean(5)
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetSecuritiesAsync({activeOnly}) failed: {ex}");
            throw new InvalidOperationException("Failed to load securities via stored procedures.", ex);
        }
    }

    public async Task<PortfolioOverviewDto> GetAccountOverviewAsync(int accountId)
    {
        try
        {
            return await BuildOverviewAsync(accountId, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetAccountOverviewAsync({accountId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to build overview for account {accountId} via stored procedures.", ex);
        }
    }

    public async Task<PortfolioOverviewDto> GetUserOverviewAsync(int userId)
    {
        try
        {
            return await BuildOverviewAsync(null, userId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.GetUserOverviewAsync({userId}) failed: {ex}");
            throw new InvalidOperationException($"Failed to build overview for user {userId} via stored procedures.", ex);
        }
    }

    private async Task<(decimal realized, decimal invested)> CalculateRealizedAndInvestedAsync(int accountId, DateTime? asOfDate)
    {
        const string sql = @"
SELECT o.SecurityID, o.OrderType, t.Quantity, t.Price, t.TradeDate
FROM Trades t
INNER JOIN Orders o ON t.OrderID = o.OrderID
WHERE o.AccountID = @AccountID
  AND (@AsOfDate IS NULL OR t.TradeDate <= @AsOfDate);";

        using var conn = _connectionFactory.CreateOpenConnection();
        using var cmd = new SqlCommand(sql, (SqlConnection)conn);
        cmd.Parameters.AddWithValue("@AccountID", accountId);
        var asOfParam = cmd.Parameters.Add("@AsOfDate", SqlDbType.DateTime);
        asOfParam.Value = (object?)asOfDate ?? DBNull.Value;

        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        var trades = new List<TradeRow>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            trades.Add(new TradeRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetDateTime(4)));
        }

        decimal realized = 0m;
        decimal invested = 0m;

        foreach (var group in trades.GroupBy(t => t.SecurityId))
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

    private static async Task<decimal> GetHoldingQuantityAsync(SqlConnection connection, int accountId, int securityId)
    {
        const string sql = @"SELECT Quantity FROM Holdings WHERE AccountID = @AccountID AND SecurityID = @SecurityID;";
        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@AccountID", accountId);
        cmd.Parameters.AddWithValue("@SecurityID", securityId);
        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        if (result == null || result == DBNull.Value)
        {
            return 0m;
        }
        return Convert.ToDecimal(result);
    }

    private static decimal SafeGetDecimal(SqlDataReader reader, string column, decimal? fallback = null)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? (fallback ?? 0m) : reader.GetDecimal(ordinal);
    }

    private static decimal SafeGetDecimal(SqlDataReader reader, int ordinal, decimal? fallback = null)
    {
        return reader.IsDBNull(ordinal) ? (fallback ?? 0m) : reader.GetDecimal(ordinal);
    }

    private static string SafeGetString(SqlDataReader reader, string column, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
    }

    private static string SafeGetString(SqlDataReader reader, int ordinal, string fallback = "")
    {
        return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
    }

    private static bool TryGetOrdinal(SqlDataReader reader, string column, out int ordinal)
    {
        try
        {
            ordinal = reader.GetOrdinal(column);
            return true;
        }
        catch (IndexOutOfRangeException)
        {
            ordinal = -1;
            return false;
        }
    }

    private async Task<PortfolioOverviewDto> BuildOverviewAsync(int? accountId, int? userId)
    {
        var filter = accountId.HasValue ? "a.AccountID = @AccountID" : "a.UserID = @UserID";
        var securities = new List<PortfolioSecuritySummaryDto>();

        const string holdingsSqlTemplate = @"
SELECT hv.SecurityID, hv.Ticker, hv.CompanyName, hv.Quantity, hv.AvgCost, hv.LatestClosePrice
FROM v_AccountHoldingsValue hv
JOIN Accounts a ON a.AccountID = hv.AccountID
WHERE {FILTER} AND hv.Quantity <> 0;";

        const string cashSqlTemplate = @"
SELECT
    SUM(cl.Amount) AS CashBalance,
    SUM(CASE WHEN LOWER(cl.Type) LIKE 'deposit%' OR LOWER(cl.Type) LIKE 'dividend%' OR LOWER(cl.Type) LIKE 'credit%' THEN cl.Amount ELSE 0 END) AS Deposits,
    SUM(CASE WHEN LOWER(cl.Type) LIKE 'withdraw%' OR LOWER(cl.Type) LIKE 'fee%' OR LOWER(cl.Type) LIKE 'debit%' THEN cl.Amount ELSE 0 END) AS Withdrawals
FROM CashLedger cl
JOIN Accounts a ON a.AccountID = cl.AccountID
WHERE {FILTER};";

        const string tradesSqlTemplate = @"
SELECT o.SecurityID, o.OrderType, t.Quantity, t.Price
FROM Trades t
JOIN Orders o ON t.OrderID = o.OrderID
JOIN Accounts a ON a.AccountID = o.AccountID
WHERE {FILTER};";

        try
        {
            using var conn = _connectionFactory.CreateOpenConnection();

            // Holdings and pricing
            var holdingsCmd = new SqlCommand(holdingsSqlTemplate.Replace("{FILTER}", filter), (SqlConnection)conn);
            AddFilterParam(holdingsCmd, accountId, userId);
            using (var reader = await holdingsCmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                var rows = new List<(int SecurityId, string Ticker, string Company, decimal Qty, decimal AvgCost, decimal Price)>();
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    rows.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetDecimal(3),
                        reader.GetDecimal(4),
                        reader.GetDecimal(5)
                    ));
                }
                securities = rows
                    .GroupBy(r => r.SecurityId)
                    .Select(g =>
                    {
                        var qty = g.Sum(x => x.Qty);
                        var costSum = g.Sum(x => x.Qty * x.AvgCost);
                        var avgCost = qty != 0 ? costSum / qty : 0m;
                        var price = g.First().Price;
                        var mv = qty * price;
                        var upl = (price - avgCost) * qty;
                        var first = g.First();
                        return new PortfolioSecuritySummaryDto
                        {
                            SecurityId = g.Key,
                            Ticker = first.Ticker,
                            CompanyName = first.Company,
                            Quantity = qty,
                            AvgCost = avgCost,
                            LatestPrice = price,
                            MarketValue = mv,
                            UnrealizedPL = upl
                        };
                    })
                    .OrderByDescending(s => s.MarketValue)
                    .ToList();
            }

            var totalSecurityValue = securities.Sum(s => s.MarketValue);
            var totalUnrealized = securities.Sum(s => s.UnrealizedPL);

            // Cash and contributions
            decimal cashBalance = 0m;
            decimal deposits = 0m;
            decimal withdrawals = 0m;
            var cashCmd = new SqlCommand(cashSqlTemplate.Replace("{FILTER}", filter), (SqlConnection)conn);
            AddFilterParam(cashCmd, accountId, userId);
            using (var reader = await cashCmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (await reader.ReadAsync().ConfigureAwait(false))
                {
                    cashBalance = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                    deposits = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                    withdrawals = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                }
            }
            var netContribution = deposits - withdrawals;

            // Trades for realized P/L
            var trades = new List<(int SecurityId, string OrderType, decimal Qty, decimal Price)>();
            var tradesCmd = new SqlCommand(tradesSqlTemplate.Replace("{FILTER}", filter), (SqlConnection)conn);
            AddFilterParam(tradesCmd, accountId, userId);
            using (var reader = await tradesCmd.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    trades.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetDecimal(2),
                        reader.GetDecimal(3)
                    ));
                }
            }

            decimal realized = 0m;
            foreach (var group in trades.GroupBy(t => t.SecurityId))
            {
                var buys = group.Where(t => string.Equals(t.OrderType, "BUY", StringComparison.OrdinalIgnoreCase)).ToList();
                var sells = group.Where(t => string.Equals(t.OrderType, "SELL", StringComparison.OrdinalIgnoreCase)).ToList();
                var buyQty = buys.Sum(b => b.Qty);
                var buyCost = buys.Sum(b => b.Qty * b.Price);
                var avgCost = buyQty > 0 ? buyCost / buyQty : 0m;
                realized += sells.Sum(s => s.Qty * (s.Price - avgCost));
            }

            var totalPortfolioValue = totalSecurityValue + cashBalance;
            var totalReturnPct = netContribution > 0 ? (totalPortfolioValue - netContribution) / netContribution : (decimal?)null;

            return new PortfolioOverviewDto
            {
                AccountId = accountId,
                UserId = userId,
                Securities = securities,
                TotalSecurityValue = totalSecurityValue,
                CashBalance = cashBalance,
                TotalPortfolioValue = totalPortfolioValue,
                TotalUnrealizedPL = totalUnrealized,
                TotalRealizedPL = realized,
                NetContribution = netContribution,
                TotalReturnPct = totalReturnPct
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SpPortfolioService.BuildOverviewAsync failed: {ex}");
            throw;
        }
    }

    private static void AddFilterParam(SqlCommand cmd, int? accountId, int? userId)
    {
        if (accountId.HasValue)
        {
            cmd.Parameters.AddWithValue("@AccountID", accountId.Value);
        }
        else
        {
            cmd.Parameters.AddWithValue("@UserID", userId!.Value);
        }
    }

    private sealed record TradeRow(int SecurityId, string OrderType, decimal Quantity, decimal Price, DateTime TradeDate);
}
