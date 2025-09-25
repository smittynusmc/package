using StargateAPI.Business.Data;

public interface ILogBuffer
{
    void Add(LogEntry entry);
    IReadOnlyList<LogEntry> Drain();
}

public class LogBuffer : ILogBuffer
{
    private readonly List<LogEntry> _items = new();
    public void Add(LogEntry e) => _items.Add(e);
    public IReadOnlyList<LogEntry> Drain()
    {
        var copy = _items.ToArray();
        _items.Clear();
        return copy;
    }
}
