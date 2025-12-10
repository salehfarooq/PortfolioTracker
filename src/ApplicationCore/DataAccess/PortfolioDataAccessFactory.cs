using System;

namespace ApplicationCore.DataAccess;

public static class PortfolioDataAccessFactory
{
    public static IPortfolioDataAccess Create(
        string type,
        PortfolioDbContext efContext,
        ISqlConnectionFactory? spConnectionFactory)
    {
        var normalized = type?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "ef" or "linq" => CreateEf(efContext),
            "sp" or "sproc" => CreateSp(spConnectionFactory),
            _ => throw new ArgumentException($"Unknown data access type: {type}", nameof(type))
        };
    }

    private static IPortfolioDataAccess CreateEf(PortfolioDbContext efContext)
    {
        if (efContext is null)
        {
            throw new ArgumentNullException(nameof(efContext), "EF context is required for EF data access.");
        }

        var efType = Type.GetType("Infrastructure.EF.DataAccess.EfPortfolioDataAccess, Infrastructure.EF");
        if (efType is null)
        {
            throw new InvalidOperationException("EF data access implementation is not available.");
        }

        if (Activator.CreateInstance(efType, efContext) is not IPortfolioDataAccess instance)
        {
            throw new InvalidOperationException("Failed to create EF data access implementation.");
        }

        return instance;
    }

    private static IPortfolioDataAccess CreateSp(ISqlConnectionFactory? spFactory)
    {
        if (spFactory is null)
        {
            throw new ArgumentNullException(nameof(spFactory), "SQL connection factory is required for stored-procedure data access.");
        }

        var spType = Type.GetType("Infrastructure.SP.DataAccess.SpPortfolioDataAccess, Infrastructure.SP");
        if (spType is null)
        {
            throw new InvalidOperationException("Stored-procedure data access implementation is not available.");
        }

        if (Activator.CreateInstance(spType, spFactory) is not IPortfolioDataAccess instance)
        {
            throw new InvalidOperationException("Failed to create stored-procedure data access implementation.");
        }

        return instance;
    }
}
