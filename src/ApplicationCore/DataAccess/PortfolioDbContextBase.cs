using Microsoft.EntityFrameworkCore;

namespace ApplicationCore.DataAccess;

public abstract class PortfolioDbContext : DbContext
{
    protected PortfolioDbContext(DbContextOptions options)
        : base(options)
    {
    }
}
