// Infrastructure/Logging/SqliteLogWriter.cs
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;
using StargateAPI.Infrastructure.Concurrency;

public class SqliteLogWriter : ILogWriter
{
    private readonly string _cs;
    public SqliteLogWriter(IConfiguration config)
    {
        _cs = config.GetConnectionString("StarbaseApiDatabase")
             ?? "Data Source=stargate.db;Pooling=True;";
    }

    public async Task WriteAsync(LogEntry e, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO [Log]
(UtcTs, Level, Category, Message, CorrelationId, User, Path, Operation, ResponseCode, DurationMs, Data, Exception)
VALUES (@UtcTs,@Level,@Category,@Message,@CorrelationId,@User,@Path,@Operation,@ResponseCode,@DurationMs,@Data,@Exception);";

        await SqliteWriteLock.Semaphore.WaitAsync(ct);
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_cs);
            await conn.OpenAsync(ct);
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA busy_timeout=3000;";
                await pragma.ExecuteNonQueryAsync(ct);
            }
            await Dapper.SqlMapper.ExecuteAsync(conn,
                new Dapper.CommandDefinition(sql, e, cancellationToken: ct, commandTimeout: 3));
        }
        finally
        {
            SqliteWriteLock.Semaphore.Release();
        }
    }
}
