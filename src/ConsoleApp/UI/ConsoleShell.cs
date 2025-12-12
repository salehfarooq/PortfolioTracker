using ApplicationCore.DataAccess;
using ApplicationCore.DTOs;
using ApplicationCore.Enums;
using ApplicationCore.Services;
using Infrastructure.SP.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp.UI;

internal class ConsoleShell
{
    private const string DefaultConnection = "Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=Muhammadsaleh1@;TrustServerCertificate=True;";
    private string _connectionString = DefaultConnection;
    private string _backend = "ef";
    private IPortfolioDataAccess? _dal;
    private AccountSummaryDto? _selectedAccount;
    private bool _seedAttempted;

    public async Task RunAsync(string[] args)
    {
        ParseArgs(args);
        await EnsureBackendAsync(interactive: true);
        await MainMenuAsync();
    }

    private void ParseArgs(string[] args)
    {
        if (args.Length > 0)
        {
            _backend = args[0].Trim().ToLowerInvariant();
        }

        var envConn = Environment.GetEnvironmentVariable("PORTFOLIO_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envConn))
        {
            _connectionString = envConn;
        }
    }

    private async Task EnsureBackendAsync(bool interactive)
    {
        if (interactive)
        {
            await BackendSelectionMenuAsync();
        }
        else
        {
            await BuildDalAsync(_backend);
        }
    }

    private async Task BackendSelectionMenuAsync()
    {
        while (true)
        {
            UiPrinter.Header("Welcome to Portfolio Console");
            Console.WriteLine($"Current backend: {_backend.ToUpperInvariant()}");
            Console.WriteLine($"Connection: {MaskConnectionString(_connectionString)}");
            Console.WriteLine();
            var choice = UiPrompts.Menu("Choose backend", new[]
            {
                "Use EF / LINQ",
                "Use Stored Procedures",
                "Change connection string",
                "Continue"
            });

            switch (choice)
            {
                case 1:
                    _backend = "ef";
                    break;
                case 2:
                    _backend = "sproc";
                    break;
                case 3:
                    _connectionString = UiPrompts.ReadString("Enter SQL connection string", _connectionString);
                    break;
                case 4:
                    try
                    {
                        await BuildDalAsync(_backend);
                        return;
                    }
                    catch (Exception ex)
                    {
                        UiPrinter.Error($"Failed to initialize backend: {ex.Message}");
                    }
                    break;
            }
        }
    }

    private async Task MainMenuAsync()
    {
        while (true)
        {
            UiPrinter.Header("Portfolio Console");
            UiPrinter.Status($"Backend: {_backend.ToUpperInvariant()} | Connection: {MaskConnectionString(_connectionString)} | Account: {(_selectedAccount?.AccountName ?? "None")}");
            var choice = UiPrompts.Menu("Main Menu", new[]
            {
                "Select account",
                "Add account",
                "Delete user",
                "View securities",
                "View holdings",
                "Portfolio snapshot",
                "Recent trades",
                "Recent cash activity",
                "Top assets",
                "Security return series",
                "Place order",
                "Switch backend",
                "Exit"
            });

            switch (choice)
            {
                case 1:
                    await SelectAccountAsync();
                    break;
                case 2:
                    await AddAccountAsync();
                    break;
                case 3:
                    await DeleteUserAsync();
                    break;
                case 4:
                    await ShowSecuritiesAsync();
                    break;
                case 5:
                    await ShowHoldingsAsync();
                    break;
                case 6:
                    await ShowSnapshotAsync();
                    break;
                case 7:
                    await ShowRecentTradesAsync();
                    break;
                case 8:
                    await ShowCashAsync();
                    break;
                case 9:
                    await ShowTopAssetsAsync();
                    break;
                case 10:
                    await ShowReturnSeriesAsync();
                    break;
                case 11:
                    await PlaceOrderAsync();
                    break;
                case 12:
                    await SwitchBackendAsync();
                    break;
                case 13:
                    UiPrinter.Info("Goodbye!");
                    return;
            }
        }
    }

    private async Task SwitchBackendAsync()
    {
        _selectedAccount = null;
        await BackendSelectionMenuAsync();
    }

    private async Task SelectAccountAsync()
    {
        if (_dal is null)
        {
            UiPrinter.Error("Backend not initialized.");
            return;
        }

        try
        {
            var accounts = await _dal.AccountService.GetAccountsAsync();
            if (accounts.Count == 0)
            {
                UiPrinter.Warn("No active accounts found.");
                UiPrompts.Pause();
                return;
            }

            UiPrinter.Table(
                new[] { "#", "Account", "User", "Type", "Active", "Created" },
                accounts.Select((a, idx) => new[]
                {
                    (idx + 1).ToString(),
                    a.AccountName,
                    a.UserName,
                    a.AccountType,
                    a.IsActive ? "Yes" : "No",
                    a.CreatedDate.ToShortDateString()
                }).ToList());

            var choice = UiPrompts.ReadInt("Select account #", 1, 1, accounts.Count);
            _selectedAccount = accounts[choice - 1];
            UiPrinter.Info($"Selected account: {_selectedAccount.AccountName}");
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load accounts: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task ShowSecuritiesAsync()
    {
        if (_dal is null)
        {
            UiPrinter.Error("Backend not initialized.");
            UiPrompts.Pause();
            return;
        }
        try
        {
            var scopeChoice = UiPrompts.Menu("Securities scope", new[] { "Active only", "All securities" });
            var activeOnly = scopeChoice == 1;

            var securities = await _dal!.PortfolioService.GetSecuritiesAsync(activeOnly);
            if (securities.Count == 0)
            {
                UiPrinter.Warn("No securities found.");
                UiPrompts.Pause();
                return;
            }

            var ordered = securities
                .Select(s => new { Security = s, Synthetic = IsSyntheticSecurity(s) })
                .OrderBy(x => x.Synthetic)
                .ThenBy(x => x.Security.Ticker)
                .ToList();

            var pageSize = 15;
            var page = 0;
            var total = ordered.Count;

            while (true)
            {
                var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
                page = Math.Clamp(page, 0, totalPages - 1);

                var pageItems = ordered.Skip(page * pageSize).Take(pageSize).ToList();

                UiPrinter.Header("Securities");
                UiPrinter.Info($"Showing page {page + 1}/{totalPages} (size {pageSize}) • Total {total} securities");
                UiPrinter.Table(
                    new[] { "ID", "Ticker", "Company", "Sector", "Listed", "Active", "Synthetic" },
                    pageItems.Select(x => new[]
                    {
                        x.Security.SecurityId.ToString(),
                        x.Security.Ticker,
                        x.Security.CompanyName,
                        x.Security.Sector,
                        x.Security.ListedIn,
                        x.Security.IsActive ? "Yes" : "No",
                        x.Synthetic ? "Yes" : "No"
                    }).ToList());

                var options = new List<(string Label, Action Action)>
                {
                    ("Change page size", () =>
                    {
                        pageSize = UiPrompts.ReadInt("Page size", pageSize, 1, 200);
                        page = 0;
                    }),
                    ("Back to menu", () => page = -1)
                };
                if (page > 0)
                {
                    options.Insert(0, ("Previous page", () => page--));
                }
                if (page < totalPages - 1)
                {
                    options.Insert(options.Count - 1, ("Next page", () => page++));
                }

                var selected = UiPrompts.Menu("Navigation", options.Select(o => o.Label).ToArray());
                options[selected - 1].Action();
                if (page == -1)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load securities: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task AddAccountAsync()
    {
        if (_dal is null)
        {
            UiPrinter.Error("Backend not initialized.");
            UiPrompts.Pause();
            return;
        }

        var username = UiPrompts.ReadString("Username", "user" + DateTime.UtcNow.Ticks.ToString()[^4..]);
        var fullName = UiPrompts.ReadString("Full name", "New User");
        var email = UiPrompts.ReadString("Email", $"{username}@example.com");
        var accountName = UiPrompts.ReadString("Account name", $"{fullName} Portfolio");
        var accountType = UiPrompts.ReadString("Account type", "Individual");

        try
        {
            var dto = new NewAccountDto
            {
                Username = username,
                FullName = fullName,
                Email = email,
                AccountName = accountName,
                AccountType = accountType
            };

            var created = await _dal.AccountService.CreateAccountAsync(dto);
            _selectedAccount = created;
            UiPrinter.Info($"Created account '{created.AccountName}' for user '{created.UserName}'. Selected it for you.");
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to create account: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task DeleteUserAsync()
    {
        if (_dal is null)
        {
            UiPrinter.Error("Backend not initialized.");
            UiPrompts.Pause();
            return;
        }

        try
        {
            var users = await _dal.AccountService.GetUsersAsync();
            if (users.Count == 0)
            {
                UiPrinter.Warn("No users found.");
                UiPrompts.Pause();
                return;
            }

            UiPrinter.Header("Users");
            UiPrinter.Table(
                new[] { "#", "User ID", "Username", "Full Name", "Email", "Accounts", "Active Accounts" },
                users.Select((u, idx) => new[]
                {
                    (idx + 1).ToString(),
                    u.UserId.ToString(),
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.AccountCount.ToString(),
                    u.ActiveAccountCount.ToString()
                }).ToList());

            var choice = UiPrompts.ReadInt("Select user # to delete", null, 1, users.Count);
            var selected = users[choice - 1];
            UiPrinter.Warn($"You are about to deactivate all accounts for user '{selected.Username}'. Historical data remains intact.");
            var confirm = UiPrompts.Menu("Confirm delete", new[] { "Yes, delete user", "Cancel" });
            if (confirm != 1)
            {
                UiPrinter.Warn("Delete cancelled.");
                UiPrompts.Pause();
                return;
            }

            var success = await _dal.AccountService.DeleteUserAsync(selected.UserId);
            if (success && _selectedAccount?.UserName == selected.Username)
            {
                _selectedAccount = null;
            }

            UiPrinter.Info("User deleted (accounts marked inactive).");
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to delete user: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private static bool IsSyntheticSecurity(SecurityDto s)
    {
        var text = $"{s.Ticker} {s.CompanyName} {s.ListedIn}".ToLowerInvariant();
        return text.Contains("synthetic") || text.Contains("demo") || text.Contains("test");
    }

    private async Task SeedSampleDataAsync()
    {
        if (_dal is null)
        {
            return;
        }

        try
        {
            var existing = await _dal.AccountService.GetAccountsAsync();
            if (existing.Any())
            {
                return;
            }

            var securities = await _dal.PortfolioService.GetSecuritiesAsync(true);
            var usable = securities
                .Select(s => new { Sec = s, Synthetic = IsSyntheticSecurity(s) })
                .OrderBy(x => x.Synthetic)
                .ThenBy(x => x.Sec.SecurityId)
                .Take(4)
                .Select(x => x.Sec)
                .ToList();

            if (usable.Count == 0)
            {
                return;
            }

            var samples = new[]
            {
                new NewAccountDto
                {
                    Username = "demo_user1",
                    FullName = "Demo User 1",
                    Email = "demo_user1@example.com",
                    AccountName = "Demo Growth Portfolio",
                    AccountType = "Individual"
                },
                new NewAccountDto
                {
                    Username = "demo_user2",
                    FullName = "Demo User 2",
                    Email = "demo_user2@example.com",
                    AccountName = "Demo Income Portfolio",
                    AccountType = "Individual"
                }
            };

            var createdAccounts = new List<AccountSummaryDto>();
            foreach (var sample in samples)
            {
                try
                {
                    var created = await _dal.AccountService.CreateAccountAsync(sample);
                    createdAccounts.Add(created);
                }
                catch
                {
                    // ignore seeding failures per account
                }
            }

            if (!createdAccounts.Any())
            {
                return;
            }

            var rnd = new Random();
            foreach (var account in createdAccounts)
            {
                var qtyBase = rnd.Next(5, 15);
                foreach (var sec in usable)
                {
                    try
                    {
                        var dto = new NewOrderDto
                        {
                            AccountId = account.AccountId,
                            SecurityId = sec.SecurityId,
                            OrderType = OrderType.Buy,
                            Quantity = qtyBase + rnd.Next(1, 5),
                            Price = 50 + rnd.Next(1, 20)
                        };
                        await _dal.PortfolioService.PlaceOrderAsync(dto);
                    }
                    catch
                    {
                        // best effort
                    }
                }
            }
        }
        catch
        {
            // swallow seeding errors to avoid blocking user
        }
    }

    private async Task ShowHoldingsAsync()
    {
        if (!EnsureAccount()) return;
        try
        {
            var holdings = await _dal!.PortfolioService.GetHoldingsAsync(_selectedAccount!.AccountId);
            if (holdings.Count == 0)
            {
                UiPrinter.Warn("No holdings for this account.");
                UiPrompts.Pause();
                return;
            }

            UiPrinter.Table(
                new[] { "Ticker", "Qty", "AvgCost", "Price", "MV", "Unrealized" },
                holdings.Select(h => new[]
                {
                    h.Ticker,
                    h.Quantity.ToString("N2"),
                    h.AvgCost.ToString("N2"),
                    h.LatestPrice.ToString("N2"),
                    h.MarketValue.ToString("N2"),
                    h.UnrealizedPL.ToString("N2")
                }).ToList(),
                footer: new[]
                {
                    "TOTAL",
                    "",
                    "",
                    "",
                    holdings.Sum(h => h.MarketValue).ToString("N2"),
                    holdings.Sum(h => h.UnrealizedPL).ToString("N2")
                });
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load holdings: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task ShowSnapshotAsync()
    {
        if (!EnsureAccount()) return;

        try
        {
            var snapshot = await _dal!.PortfolioService.GetPortfolioSnapshotAsync(_selectedAccount!.AccountId, null);
            UiPrinter.Header($"Snapshot as of {snapshot.AsOfDate:g}");
            UiPrinter.Kv("Total Market Value", snapshot.TotalMarketValue.ToString("N2"));
            UiPrinter.Kv("Unrealized P/L", snapshot.TotalUnrealizedPL.ToString("N2"));
            UiPrinter.Kv("Realized P/L", snapshot.TotalRealizedPL?.ToString("N2") ?? "N/A");
            UiPrinter.Kv("Total Return %", snapshot.TotalReturnPct?.ToString("P2") ?? "N/A");

            var top = snapshot.Holdings.OrderByDescending(h => h.MarketValue).Take(5).ToList();
            if (top.Any())
            {
                UiPrinter.SubHeader("Top Holdings");
                UiPrinter.Table(
                    new[] { "Ticker", "Qty", "Price", "MV", "Unrealized" },
                    top.Select(h => new[]
                    {
                        h.Ticker,
                        h.Quantity.ToString("N2"),
                        h.LatestPrice.ToString("N2"),
                        h.MarketValue.ToString("N2"),
                        h.UnrealizedPL.ToString("N2")
                    }).ToList());
            }
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load snapshot: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task ShowRecentTradesAsync()
    {
        if (!EnsureAccount()) return;
        var take = UiPrompts.ReadInt("How many trades? (default 5)", 5, 1, 50);
        try
        {
            var trades = await _dal!.PortfolioService.GetRecentTradesAsync(_selectedAccount!.AccountId, take);
            if (trades.Count == 0)
            {
                UiPrinter.Warn("No trades found.");
                UiPrompts.Pause();
                return;
            }

            UiPrinter.Table(
                new[] { "Date", "Ticker", "Side", "Qty", "Price" },
                trades.Select(t => new[]
                {
                    t.TradeDate.ToString("g"),
                    t.Ticker,
                    t.OrderType,
                    t.Quantity.ToString("N2"),
                    t.Price.ToString("N2")
                }).ToList());
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load trades: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task ShowCashAsync()
    {
        if (!EnsureAccount()) return;
        var take = UiPrompts.ReadInt("How many cash entries? (default 5)", 5, 1, 50);
        try
        {
            var cash = await _dal!.PortfolioService.GetRecentCashActivityAsync(_selectedAccount!.AccountId, take);
            if (cash.Count == 0)
            {
                UiPrinter.Warn("No cash activity found.");
                UiPrompts.Pause();
                return;
            }

            UiPrinter.Table(
                new[] { "Date", "Type", "Amount", "Ref" },
                cash.Select(c => new[]
                {
                    c.TxnDate.ToString("g"),
                    c.Type,
                    c.Amount.ToString("N2"),
                    c.Reference
                }).ToList());
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load cash activity: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task ShowTopAssetsAsync()
    {
        if (!EnsureAccount()) return;
        var metricChoice = UiPrompts.Menu("Metric", new[] { "MarketValue", "UnrealizedPL", "ReturnPct" });
        var metric = metricChoice switch
        {
            1 => "MarketValue",
            2 => "UnrealizedPL",
            _ => "ReturnPct"
        };
        var topN = UiPrompts.ReadInt("Top N? (default 5)", 5, 1, 25);

        try
        {
            var holdings = await _dal!.PortfolioService.GetTopAssetsAsync(_selectedAccount!.AccountId, topN, metric);
            if (holdings.Count == 0)
            {
                UiPrinter.Warn("No holdings found.");
                UiPrompts.Pause();
                return;
            }

            UiPrinter.Table(
                new[] { "Ticker", "Qty", "Price", "MV", "Unrealized" },
                holdings.Select(h => new[]
                {
                    h.Ticker,
                    h.Quantity.ToString("N2"),
                    h.LatestPrice.ToString("N2"),
                    h.MarketValue.ToString("N2"),
                    h.UnrealizedPL.ToString("N2")
                }).ToList());
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load top assets: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task ShowReturnSeriesAsync()
    {
        if (!EnsureAccount()) return;

        var securityId = UiPrompts.ReadInt("Security ID", null, 1, null);
        var start = UiPrompts.ReadDate("Start date (optional, yyyy-MM-dd)", null);
        var end = UiPrompts.ReadDate("End date (optional, yyyy-MM-dd)", null);

        try
        {
            var series = await _dal!.PortfolioService.GetSecurityReturnSeriesAsync(securityId, start, end);
            if (series.Count == 0)
            {
                UiPrinter.Warn("No return data found.");
                UiPrompts.Pause();
                return;
            }

            var preview = series.Take(20).ToList();
            UiPrinter.Table(
                new[] { "Date", "Close", "Daily%", "Cum%" },
                preview.Select(p => new[]
                {
                    p.PriceDate.ToString("yyyy-MM-dd"),
                    p.ClosePrice.ToString("N2"),
                    p.DailyReturnPct?.ToString("P2") ?? "",
                    p.CumReturnApprox?.ToString("P2") ?? ""
                }).ToList());

            UiPrinter.Info($"Shown {preview.Count} of {series.Count} rows.");
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            UiPrinter.Error($"Failed to load return series: {ex.Message}");
            UiPrompts.Pause();
        }
    }

    private async Task PlaceOrderAsync()
    {
        if (!EnsureAccount()) return;

        var sideChoice = UiPrompts.Menu("Order type", new[] { "Buy", "Sell" });
        var orderType = sideChoice == 1 ? OrderType.Buy : OrderType.Sell;
        var securityId = UiPrompts.ReadInt("Security ID", null, 1, null);
        var qty = UiPrompts.ReadDecimal("Quantity", null, 0.0001m, null);
        var price = UiPrompts.ReadDecimal("Price", null, 0.0001m, null);

        UiPrinter.Info($"About to place {orderType} for Security {securityId}, Qty {qty:N4}, Price {price:N4} on Account {_selectedAccount!.AccountName}");
        var confirm = UiPrompts.Menu("Confirm", new[] { "Yes, place order", "Cancel" });
        if (confirm != 1)
        {
            UiPrinter.Warn("Order cancelled.");
            UiPrompts.Pause();
            return;
        }

        try
        {
            var dto = new NewOrderDto
            {
                AccountId = _selectedAccount!.AccountId,
                SecurityId = securityId,
                OrderType = orderType,
                Quantity = qty,
                Price = price
            };
            await _dal!.PortfolioService.PlaceOrderAsync(dto);
            UiPrinter.Info("Order submitted. Check recent trades/holdings for updates.");
            UiPrompts.Pause();
        }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException)
            {
                UiPrinter.Warn(ex.Message);
            }
            else
            {
                UiPrinter.Error($"Failed to place order: {ex.Message}");
            }
            UiPrompts.Pause();
        }
    }

    private async Task BuildDalAsync(string type)
    {
        switch (type.Trim().ToLowerInvariant())
        {
            case "ef":
            case "linq":
                var options = new DbContextOptionsBuilder<Infrastructure.EF.Generated.PortfolioDbContext>()
                    .UseSqlServer(_connectionString)
                    .Options;
                var efContext = new Infrastructure.EF.Generated.PortfolioDbContext(options);
                _dal = PortfolioDataAccessFactory.Create("ef", efContext, null);
                _backend = "ef";
                break;
            case "sproc":
            case "sp":
                var factory = new SqlConnectionFactory(_connectionString);
                _dal = PortfolioDataAccessFactory.Create("sp", null!, factory);
                _backend = "sproc";
                break;
            default:
                throw new ArgumentException("Unknown backend type.");
        }

        if (!_seedAttempted)
        {
            _seedAttempted = true;
            await SeedSampleDataAsync();
        }
    }

    private bool EnsureAccount()
    {
        if (_dal is null)
        {
            UiPrinter.Error("Backend not initialized.");
            return false;
        }
        if (_selectedAccount is null)
        {
            UiPrinter.Warn("Select an account first.");
            return false;
        }
        return true;
    }

    private static string MaskConnectionString(string conn)
    {
        if (string.IsNullOrWhiteSpace(conn)) return string.Empty;
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var masked = parts.Select(p =>
        {
            if (p.Trim().StartsWith("Password", StringComparison.OrdinalIgnoreCase))
            {
                var kv = p.Split('=', 2);
                return $"{kv[0]}=*****";
            }
            return p;
        });
        return string.Join(";", masked);
    }
}
