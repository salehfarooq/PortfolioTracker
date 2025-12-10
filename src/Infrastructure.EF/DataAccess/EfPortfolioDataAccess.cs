using ApplicationCore.DataAccess;
using ApplicationCore.Services;
using Infrastructure.EF.Generated;
using Infrastructure.EF.Services;

namespace Infrastructure.EF.DataAccess;

public class EfPortfolioDataAccess : IPortfolioDataAccess
{
    public EfPortfolioDataAccess(Infrastructure.EF.Generated.PortfolioDbContext context)
    {
        AccountService = new EfAccountService(context);
        PortfolioService = new EfPortfolioService(context);
    }

    public IAccountService AccountService { get; }

    public IPortfolioService PortfolioService { get; }
}
