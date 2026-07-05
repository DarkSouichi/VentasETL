using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ETL.Data.Helpers;

public class DatabaseHelper
{
    private readonly string _connectionString;

    public DatabaseHelper(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("SalesDB")
            ?? throw new InvalidOperationException("Cadena de conexión 'SalesDB' no encontrada.");
    }

    public SqlConnection GetConnection() => new(_connectionString);

    public async Task<int> ExecuteSpAsync(string spName, Action<SqlCommand> paramBuilder)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(spName, conn)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        paramBuilder(cmd);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExistsAsync(string sql, Action<SqlCommand> paramBuilder)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        paramBuilder(cmd);
        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value && (int)result > 0;
    }
}
