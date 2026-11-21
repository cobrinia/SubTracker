using Microsoft.Data.SqlClient;

namespace SubTracker.AdoNet;

/// <summary>
/// Створює та відкриває SqlConnection. Рядок підключення приходить з конфігурації (user secrets),
/// а не зашивається в код. З'єднання відкривається якомога пізніше і закривається якомога раніше:
/// після Dispose фізичне з'єднання повертається в пул (connection pooling).
/// </summary>
public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Рядок підключення порожній.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
