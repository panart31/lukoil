using System.Collections.ObjectModel;
using Lukoil.Client.Config;
using Lukoil.Client.Models;
using Serilog;

namespace Lukoil.Client.Services;

public sealed class LogService : ILogService
{
    private readonly ILogger _logger;
    public ObservableCollection<LogEntry> Entries { get; } = [];

    public LogService()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(AppPaths.AppLogPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public void Info(string message)
    {
        _logger.Information(message);
        Entries.Insert(0, new LogEntry { Level = "Info", Message = message });
    }

    public void Warn(string message)
    {
        _logger.Warning(message);
        Entries.Insert(0, new LogEntry { Level = "Warn", Message = message });
    }

    public void Error(string message, Exception? ex = null)
    {
        _logger.Error(ex, message);
        Entries.Insert(0, new LogEntry { Level = "Error", Message = ex is null ? message : $"{message}: {ex.Message}" });
    }
}
