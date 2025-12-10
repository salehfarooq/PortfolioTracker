using System.Data.Common;

namespace ApplicationCore.DataAccess;

public interface ISqlConnectionFactory
{
    DbConnection CreateOpenConnection();
}
