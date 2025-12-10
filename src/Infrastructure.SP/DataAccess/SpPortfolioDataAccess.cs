using ApplicationCore.DataAccess;
using ApplicationCore.Services;
using Infrastructure.SP.Services;

namespace Infrastructure.SP.DataAccess;

public class SpPortfolioDataAccess : IPortfolioDataAccess
{
    public SpPortfolioDataAccess(ISqlConnectionFactory connectionFactory)
    {
        AccountService = new SpAccountService(connectionFactory);
        PortfolioService = new SpPortfolioService(connectionFactory);
    }

    public IAccountService AccountService { get; }

    public IPortfolioService PortfolioService { get; }
}
