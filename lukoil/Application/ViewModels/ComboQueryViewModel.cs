using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lukoil.Client.Models;

namespace Lukoil.Client.ViewModels;

public partial class ComboQueryViewModel : ViewModelBase
{
    private readonly Func<string, Task<QueryResult>> _execute;
    private readonly Func<Task> _goHome;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public string ScenarioTitle { get; } = "Операционный контроль";
    public string ScenarioDescription { get; } =
        "Комбо-запрос проверяет критичные зоны за один запуск: остатки на складе, должников и общую статистику.";

    public ObservableCollection<ComboSectionViewModel> Sections { get; } = [];

    public IAsyncRelayCommand RunComboCommand { get; }
    public IAsyncRelayCommand GoHomeCommand { get; }

    public ComboQueryViewModel(Func<string, Task<QueryResult>> execute, Func<Task> goHome)
    {
        _execute = execute;
        _goHome = goHome;

        Sections.Add(new ComboSectionViewModel("Склад", "GET_INVENTORY"));
        Sections.Add(new ComboSectionViewModel("Должники", "GET_DEBTORS"));
        Sections.Add(new ComboSectionViewModel("Статистика", "GET_STATS"));

        RunComboCommand = new AsyncRelayCommand(RunComboAsync);
        GoHomeCommand = new AsyncRelayCommand(async () => await _goHome());
    }

    public async Task RunComboAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            foreach (var section in Sections)
            {
                var result = await _execute(section.Command);
                if (!result.IsSuccess)
                {
                    section.Items = null;
                    section.RowCount = 0;
                    section.Error = result.ErrorMessage ?? "Ошибка запроса";
                    continue;
                }

                var table = result.Table?.DataTable ?? new DataTable();
                section.Items = table.DefaultView;
                section.RowCount = table.Rows.Count;
                section.Error = null;
            }

            if (Sections.All(s => s.Items is null || s.RowCount == 0))
            {
                ErrorMessage = "Комбо-запрос выполнен, но сервер не вернул табличные данные.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public partial class ComboSectionViewModel(string title, string command) : ObservableObject
{
    public string Title { get; } = title;
    public string Command { get; } = command;

    [ObservableProperty]
    private DataView? _items;

    [ObservableProperty]
    private int _rowCount;

    [ObservableProperty]
    private string? _error;
}
