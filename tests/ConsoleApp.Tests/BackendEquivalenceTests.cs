using Infrastructure.EF.DataAccess;
using Infrastructure.SP.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp.Tests;

public class BackendEquivalenceTests
{
    [Fact]
    public async Task EfAndStoredProcedureBackends_ReturnSameSeededOverview_WhenIntegrationConnectionExists()
    {
        var connectionString = Environment.GetEnvironmentVariable("PORTFOLIO_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<Infrastructure.EF.Generated.PortfolioDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new Infrastructure.EF.Generated.PortfolioDbContext(options);

        var ef = new EfPortfolioDataAccess(context);
        var sp = new SpPortfolioDataAccess(new SqlConnectionFactory(connectionString));

        var account = (await ef.AccountService.GetAccountsAsync()).First();
        var efOverview = await ef.PortfolioService.GetAccountOverviewAsync(account.AccountId);
        var spOverview = await sp.PortfolioService.GetAccountOverviewAsync(account.AccountId);

        Assert.Equal(efOverview.TotalSecurityValue, spOverview.TotalSecurityValue);
        Assert.Equal(efOverview.CashBalance, spOverview.CashBalance);
        Assert.Equal(efOverview.TotalRealizedPL, spOverview.TotalRealizedPL);
    }
}

