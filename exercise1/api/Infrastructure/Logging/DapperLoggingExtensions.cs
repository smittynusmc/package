// Infrastructure/Logging/DapperLoggingExtensions.cs
using System.Data;
using System.Diagnostics;
using Dapper;
using StargateAPI.Business.Data;

public static class DapperLoggingExtensions
{
    public static async Task<IEnumerable<T>> QueryLoggedAsync<T>(
        this IDbConnection conn, ILogBuffer buffer, string sql, object? param = null,
        string? corrId = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await conn.QueryAsync<T>(
                new CommandDefinition(sql, param, cancellationToken: ct));
            buffer.Add(new LogEntry {
                Category = "Dapper", Operation = "Query", CorrelationId = corrId,
                Message = $"rows={result.Count()}", Data = ILogWriter.Truncate(sql),
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
            return result;
        }
        catch (Exception ex)
        {
            buffer.Add(new LogEntry {
                Level = "Error", Category = "Dapper", Operation = "Query", CorrelationId = corrId,
                Message = ex.Message, Exception = ex.ToString(), Data = ILogWriter.Truncate(sql),
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
            throw;
        }
    }
}
