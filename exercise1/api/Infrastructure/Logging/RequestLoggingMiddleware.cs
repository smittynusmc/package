// Infrastructure/Logging/RequestLoggingMiddleware.cs
using System.Diagnostics;
using StargateAPI.Business.Data;
using StargateAPI.Infrastructure.Concurrency;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ILogBuffer buffer, ILogWriter writer)
    {
        var sw = Stopwatch.StartNew();
        var correlationId = ctx.TraceIdentifier;
        ctx.Response.Headers["X-Correlation-Id"] = correlationId;

        try
        {
            await _next(ctx);

            buffer.Add(new LogEntry
            {
                Level = "Info",
                Category = "HTTP",
                Message = "Request completed",
                CorrelationId = correlationId,
                User = ctx.User?.Identity?.Name,
                Path = $"{ctx.Request.Method} {ctx.Request.Path}",
                ResponseCode = ctx.Response.StatusCode,
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            buffer.Add(new LogEntry
            {
                Level = "Error",
                Category = "HTTP",
                Message = ex.Message,
                Exception = ex.ToString(),
                CorrelationId = correlationId,
                User = ctx.User?.Identity?.Name,
                Path = $"{ctx.Request.Method} {ctx.Request.Path}",
                ResponseCode = StatusCodes.Status500InternalServerError,
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
            throw;
        }
        finally
        {
            var items = buffer.Drain();
            if (items.Count > 0)
            {
                try
                {
                    foreach (var e in items)
                        await writer.WriteAsync(e);
                }
                catch
                {
                    // Swallow logging errors; never block the request pipeline.
                }
            }
        }
    }
}
