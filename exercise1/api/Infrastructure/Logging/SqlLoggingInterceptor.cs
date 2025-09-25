// Infrastructure/Logging/SqlLoggingInterceptor.cs
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StargateAPI.Business.Data;

public class SqlLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogBuffer _buffer;
    private readonly IHttpContextAccessor _http;

    public SqlLoggingInterceptor(ILogBuffer buffer, IHttpContextAccessor http)
    {
        _buffer = buffer;
        _http = http;
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Add("EF", "NonQuery", command, eventData);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Add("EF", "Reader", command, eventData);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Add("EF", "Scalar", command, eventData);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var corr = _http.HttpContext?.TraceIdentifier;
        _buffer.Add(new LogEntry
        {
            Level = "Error",
            Category = "EF",
            Operation = "CommandFailed",
            CorrelationId = corr,
            Message = eventData.Exception.Message,
            Exception = eventData.Exception.ToString(),
            Data = ILogWriter.Truncate(command.CommandText),
            DurationMs = eventData.Duration.TotalMilliseconds
        });

        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void Add(string category, string op, DbCommand cmd, CommandExecutedEventData e)
    {
        var corr = _http.HttpContext?.TraceIdentifier;
        _buffer.Add(new LogEntry
        {
            Category = category,
            Operation = op,
            CorrelationId = corr,
            Message = "SQL",
            Data = ILogWriter.Truncate(cmd.CommandText),
            DurationMs = e.Duration.TotalMilliseconds
        });
    }
}
