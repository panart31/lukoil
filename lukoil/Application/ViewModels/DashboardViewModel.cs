using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lukoil.Client.Models;

namespace Lukoil.Client.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly Func<string, Task<QueryResult>> _executeCommand;
    private readonly Func<string, Task> _openScreenByCommand;

    public ObservableCollection<QuickCommandItem> QuickCommands { get; } = [];

    [ObservableProperty]
    private string _statsPreview = "Сводка пока не загружена.";

    [ObservableProperty]
    private DataView? _statsItems;

    public IAsyncRelayCommand StatsCommand { get; }

    public DashboardViewModel(Func<string, Task<QueryResult>> executeCommand, Func<string, Task> openScreenByCommand)
    {
        _executeCommand = executeCommand;
        _openScreenByCommand = openScreenByCommand;
        StatsCommand = new AsyncRelayCommand(RefreshStatsAsync);

        var commands = new[]
        {
            ("Товары", "GET_ALL_PRODUCTS"),
            ("Склад", "GET_INVENTORY"),
            ("Поставщики", "GET_SUPPLIERS"),
            ("Продажи", "GET_SALES|0")
        };

        foreach (var (title, commandText) in commands)
        {
            QuickCommands.Add(new QuickCommandItem
            {
                Title = title,
                CommandText = commandText,
                ExecuteCommand = new AsyncRelayCommand(async () => await _openScreenByCommand(commandText))
            });
        }
    }

    public async Task RefreshStatsAsync()
    {
        var result = await _executeCommand("GET_STATS");
        if (!result.IsSuccess)
        {
            StatsPreview = result.ErrorMessage ?? "Не удалось загрузить сводку.";
            StatsItems = null;
            return;
        }

        StatsItems = (result.Table?.DataTable ?? new DataTable()).DefaultView;
        StatsPreview = string.IsNullOrWhiteSpace(result.RawResponse)
            ? "Сводка получена, но сервер вернул пустой ответ."
            : result.RawResponse;
    }
}
