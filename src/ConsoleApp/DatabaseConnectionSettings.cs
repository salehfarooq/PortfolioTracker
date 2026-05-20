using Microsoft.Data.SqlClient;

namespace ConsoleApp;

internal sealed record DatabaseConnectionSettings(
    string Server,
    string Database,
    string User,
    string Password,
    string? FullConnectionString)
{
    public const string DefaultServer = "localhost,1433";
    public const string DefaultDatabase = "PortfolioDB";
    public const string DefaultUser = "sa";

    public string ToConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(FullConnectionString))
        {
            return FullConnectionString;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            UserID = User,
            Password = Password,
            TrustServerCertificate = true,
            Encrypt = true
        };

        return builder.ConnectionString;
    }

    public DatabaseConnectionSettings WithCredentials(string server, string database, string user, string password)
    {
        return new DatabaseConnectionSettings(server, database, user, password, null);
    }

    public static DatabaseConnectionSettings FromEnvironment(CliOptions options)
    {
        var fullConnection = Environment.GetEnvironmentVariable("PORTFOLIO_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fullConnection))
        {
            var builder = new SqlConnectionStringBuilder(fullConnection);
            return new DatabaseConnectionSettings(
                ValueOrDefault(builder.DataSource, options.Server, DefaultServer),
                ValueOrDefault(builder.InitialCatalog, options.Database, DefaultDatabase),
                ValueOrDefault(builder.UserID, options.User, DefaultUser),
                builder.Password ?? string.Empty,
                fullConnection);
        }

        return new DatabaseConnectionSettings(
            options.Server ?? Environment.GetEnvironmentVariable("PORTFOLIO_DB_SERVER") ?? DefaultServer,
            options.Database ?? Environment.GetEnvironmentVariable("PORTFOLIO_DB_NAME") ?? DefaultDatabase,
            options.User ?? Environment.GetEnvironmentVariable("PORTFOLIO_DB_USER") ?? DefaultUser,
            Environment.GetEnvironmentVariable("PORTFOLIO_DB_PASSWORD") ?? string.Empty,
            null);
    }

    public static string Mask(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "*****";
        }

        return builder.ConnectionString;
    }

    private static string ValueOrDefault(string? primary, string? secondary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        if (!string.IsNullOrWhiteSpace(secondary))
        {
            return secondary;
        }

        return fallback;
    }
}

