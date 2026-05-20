using Microsoft.Data.SqlClient;
using System.Data;

namespace Infrastructure.SP.DataAccess;

internal static class SqlCommandExtensions
{
    public static SqlParameter AddInt(this SqlCommand command, string name, int value)
    {
        return Add(command, name, SqlDbType.Int, value);
    }

    public static SqlParameter AddBool(this SqlCommand command, string name, bool value)
    {
        return Add(command, name, SqlDbType.Bit, value);
    }

    public static SqlParameter AddDecimal(this SqlCommand command, string name, decimal value, byte precision = 19, byte scale = 6)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = value;
        return parameter;
    }

    public static SqlParameter AddDateTime(this SqlCommand command, string name, DateTime? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.DateTime2);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
        return parameter;
    }

    public static SqlParameter AddString(this SqlCommand command, string name, string value, int size)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = value;
        return parameter;
    }

    public static SqlParameter AddFixedAnsiString(this SqlCommand command, string name, string value, int size)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Char, size);
        parameter.Value = value;
        return parameter;
    }

    public static SqlParameter AddBytes(this SqlCommand command, string name, byte[] value, int size)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.VarBinary, size);
        parameter.Value = value;
        return parameter;
    }

    private static SqlParameter Add<T>(SqlCommand command, string name, SqlDbType type, T value)
    {
        var parameter = command.Parameters.Add(name, type);
        parameter.Value = value!;
        return parameter;
    }
}
