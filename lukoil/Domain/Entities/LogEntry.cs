namespace Lukoil.Client.Models;

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Level { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
}
