// Infrastructure/Logging/ILogWriter.cs
// Writing logs to avoid any chance of the EF interceptor recursively logging itself.
// Declares the single method used everywhere to write a log row (plus a small safe string-truncator), letting the rest of the app log without caring how/where logs are stored.
using StargateAPI.Business.Data;

public interface ILogWriter
{
    Task WriteAsync(LogEntry e, CancellationToken ct = default);
    static string Truncate(string? s, int max = 4000)
        => string.IsNullOrEmpty(s) ? s ?? "" : (s.Length <= max ? s : s[..max]);
}
