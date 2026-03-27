using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lukoil.Client.Models;
using Lukoil.Client.Services;

namespace Lukoil.Client.ViewModels;

public partial class ConsoleViewModel : ViewModelBase
{
    private readonly Func<string, Task<QueryResult>> _execute;

    [ObservableProperty]
    private string _manualCommand = string.Empty;

    [ObservableProperty]
    private string _rawResponse = string.Empty;

    public IReadOnlyCollection<LogEntry> Logs => _logService.Entries;

    private readonly ILogService _logService;

    public IAsyncRelayCommand SendCommandCommand { get; }

    public ConsoleViewModel(Func<string, Task<QueryResult>> execute, ILogService logService)
    {
        _execute = execute;
        _logService = logService;
        SendCommandCommand = new AsyncRelayCommand(SendManualAsync);
    }

    private async Task SendManualAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualCommand))
        {
            return;
        }

        var result = await _execute(ManualCommand);
        RawResponse = result.RawResponse;
    }
}
