using System.Data.Common;
using ApplicationCore.DataAccess;
using Microsoft.Data.SqlClient;

namespace Infrastructure.SP.DataAccess;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DbConnection CreateOpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
