using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApplicationCore.DTOs;

namespace ApplicationCore.Services;

public interface IPortfolioService
{
    Task<PortfolioSnapshotDto> GetPortfolioSnapshotAsync(int accountId, DateTime? asOfDate);
    Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(int accountId);
    Task PlaceOrderAsync(NewOrderDto order);
    Task<IReadOnlyList<ReturnPointDto>> GetSecurityReturnSeriesAsync(int securityId, DateTime? start, DateTime? end);
    Task<IReadOnlyList<TradeSummaryDto>> GetRecentTradesAsync(int accountId, int take);
    Task<IReadOnlyList<CashLedgerEntryDto>> GetRecentCashActivityAsync(int accountId, int take);
    Task<IReadOnlyList<HoldingDto>> GetTopAssetsAsync(int accountId, int topN, string metric);
    Task<decimal?> GetVolatilityAsync(int securityId, DateTime? start, DateTime? end);
    Task<IReadOnlyList<SecurityDto>> GetSecuritiesAsync(bool activeOnly);
    Task<PortfolioOverviewDto> GetAccountOverviewAsync(int accountId);
    Task<PortfolioOverviewDto> GetUserOverviewAsync(int userId);
}
