using ApplicationCore.Services;

namespace ApplicationCore.DataAccess;

public interface IPortfolioDataAccess
{
    IAccountService AccountService { get; }
    IPortfolioService PortfolioService { get; }
}
