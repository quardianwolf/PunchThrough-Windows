namespace PunchThrough.Models;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Debug
}

public class LogEntry
{
    public DateTime Timestamp { get; }
    public string Message { get; }
    public LogLevel Level { get; }

    public LogEntry(string message, LogLevel level = LogLevel.Info)
    {
        Timestamp = DateTime.Now;
        Message = message;
        Level = level;
    }

    public string LevelIcon => Level switch
    {
        LogLevel.Info => "\u2139",      // ℹ
        LogLevel.Warning => "\u26A0",   // ⚠
        LogLevel.Error => "\u2716",     // ✖
        LogLevel.Debug => "\uD83D\uDC1B", // 🐛
        _ => "\u2139"
    };
}
