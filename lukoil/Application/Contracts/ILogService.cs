using System.Collections.ObjectModel;
using Lukoil.Client.Models;

namespace Lukoil.Client.Services;

public interface ILogService
{
    ObservableCollection<LogEntry> Entries { get; }
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
