// Infrastructure/Logging/LoggingBehavior.cs
using System.Diagnostics;
using System.Text.Json;
using MediatR;
using StargateAPI.Business.Data;

public class LoggingBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : notnull
{
    private readonly ILogBuffer _buffer;
    private readonly IHttpContextAccessor _http;

    public LoggingBehavior(ILogBuffer buffer, IHttpContextAccessor http)
    {
        _buffer = buffer;
        _http = http;
    }

    public async Task<TRes> Handle(TReq request, RequestHandlerDelegate<TRes> next, CancellationToken ct)
    {
        var corr = _http.HttpContext?.TraceIdentifier;
        var name = typeof(TReq).Name;
        var sw = Stopwatch.StartNew();

        _buffer.Add(new LogEntry {
            Category = "MediatR", Operation = name, CorrelationId = corr, Message = "Start",
            Data = ILogWriter.Truncate(JsonSerializer.Serialize(request))
        });

        try
        {
            var response = await next();

            _buffer.Add(new LogEntry {
                Category = "MediatR", Operation = name, CorrelationId = corr, Message = "End",
                DurationMs = sw.Elapsed.TotalMilliseconds
            });

            return response;
        }
        catch (Exception ex)
        {
            _buffer.Add(new LogEntry {
                Level = "Error", Category = "MediatR", Operation = name, CorrelationId = corr,
                Message = ex.Message, Exception = ex.ToString(), DurationMs = sw.Elapsed.TotalMilliseconds
            });
            throw;
        }
    }
}
