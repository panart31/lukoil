using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lukoil.Client.Models;

namespace Lukoil.Client.ViewModels;

public partial class DataScreenViewModel : ViewModelBase
{
    private readonly Func<string, Task<QueryResult>> _execute;
    private readonly Func<Task> _goHome;
    private DataTable? _sourceTable;
    private CancellationTokenSource? _idFilterDebounceCts;
    private int _refreshVersion;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _command;

    [ObservableProperty]
    private bool _supportsIdFilter;

    [ObservableProperty]
    private bool _supportsDateRange;

    [ObservableProperty]
    private string _idFilter = "0";

    [ObservableProperty]
    private string _dateFrom = string.Empty;

    [ObservableProperty]
    private string _dateTo = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _emptyStateMessage = "Нет данных для отображения";

    [ObservableProperty]
    private DataView? _items;

    [ObservableProperty]
    private int _recordCount;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand GoHomeCommand { get; }

    public DataScreenViewModel(string title, string command, Func<string, Task<QueryResult>> execute, Func<Task> goHome, bool supportsIdFilter = false, bool supportsDateRange = false)
    {
        _title = title;
        _command = command;
        _execute = execute;
        _goHome = goHome;
        _supportsIdFilter = supportsIdFilter;
        _supportsDateRange = supportsDateRange;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        GoHomeCommand = new AsyncRelayCommand(async () => await _goHome());
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnIdFilterChanged(string value)
    {
        if (!SupportsIdFilter)
        {
            return;
        }

        if (_sourceTable is null)
        {
            _ = DebouncedRefreshByIdAsync();
            return;
        }

        ApplyFilter();
    }

    partial void OnDateFromChanged(string value)
    {
        if (!SupportsDateRange)
        {
            return;
        }
        _ = DebouncedRefreshByIdAsync();
    }

    partial void OnDateToChanged(string value)
    {
        if (!SupportsDateRange)
        {
            return;
        }
        _ = DebouncedRefreshByIdAsync();
    }

    public async Task RefreshAsync()
    {
        var requestVersion = Interlocked.Increment(ref _refreshVersion);
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            string fullCommand = Command;

            if (SupportsDateRange && IsValidDate(DateFrom) && IsValidDate(DateTo))
            {
                fullCommand = $"{Command}|{DateFrom}|{DateTo}";
            }
            else if (SupportsIdFilter)
            {
                var id = NormalizeId(IdFilter);
                if (string.Equals(Command, "GET_CLIENT_DEBT", StringComparison.OrdinalIgnoreCase))
                {
                    fullCommand = id > 0 ? $"{Command}|{id}" : $"{Command}|ALL";
                }
                else if (string.Equals(Command, "GET_INVOICES", StringComparison.OrdinalIgnoreCase))
                {
                    fullCommand = id > 0 ? $"{Command}|{id}" : $"{Command}|ALL";
                }
                else if (string.Equals(Command, "GET_SHIPMENTS", StringComparison.OrdinalIgnoreCase))
                {
                    fullCommand = id > 0 ? $"{Command}|{id}" : $"{Command}|ALL";
                }
                else
                {
                    // Fallback: request all and filter locally
                    fullCommand = $"{Command}|0";
                }
            }

            var result = await _execute(fullCommand);

            if (!result.IsSuccess)
            {
                if (requestVersion != _refreshVersion)
                {
                    return;
                }

                Items = null;
                RecordCount = 0;
                ErrorMessage = result.ErrorMessage ?? "Ошибка выполнения запроса";
                return;
            }

            if (requestVersion != _refreshVersion)
            {
                return;
            }

            _sourceTable = result.Table?.DataTable ?? new DataTable();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        if (_sourceTable is null)
        {
            Items = null;
            RecordCount = 0;
            return;
        }

        var workingTable = BuildTableWithExactIdFilter(_sourceTable);

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Items = workingTable.DefaultView;
            RecordCount = workingTable.Rows.Count;
            return;
        }

        var term = SearchText.Trim();
        var filtered = workingTable.Clone();

        foreach (DataRow row in workingTable.Rows)
        {
            var contains = row.ItemArray.Any(cell =>
                (cell?.ToString() ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));

            if (contains)
            {
                filtered.ImportRow(row);
            }
        }

        Items = filtered.DefaultView;
        RecordCount = filtered.Rows.Count;
    }

    private DataTable BuildTableWithExactIdFilter(DataTable source)
    {
        if (!SupportsIdFilter)
        {
            return source;
        }

        var id = NormalizeId(IdFilter);
        if (id <= 0)
        {
            return source;
        }

        var filtered = source.Clone();
        var idColumn = ResolveIdColumn(source);
        if (idColumn is null)
        {
            return source;
        }

        foreach (DataRow row in source.Rows)
        {
            var valueText = (row[idColumn]?.ToString() ?? string.Empty).Trim();
            var matches = int.TryParse(valueText, out var valueInt)
                ? valueInt == id
                : string.Equals(valueText, id.ToString(), StringComparison.Ordinal);

            if (matches)
            {
                filtered.ImportRow(row);
            }
        }

        return filtered;
    }

    private static DataColumn? ResolveIdColumn(DataTable table)
    {
        var preferredNames = new[]
        {
            "id", "sale_id", "delivery_id", "product_id", "supplier_id", "client_id"
        };

        foreach (var name in preferredNames)
        {
            var byName = table.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => c.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                return byName;
            }
        }

        var bySuffix = table.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => c.ColumnName.EndsWith("_id", StringComparison.OrdinalIgnoreCase));
        if (bySuffix is not null)
        {
            return bySuffix;
        }

        // Fallback: first column that mostly contains integer values.
        foreach (DataColumn column in table.Columns)
        {
            var nonEmpty = 0;
            var intLike = 0;
            foreach (DataRow row in table.Rows)
            {
                var text = (row[column]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                nonEmpty++;
                if (int.TryParse(text, out _))
                {
                    intLike++;
                }
            }

            if (nonEmpty > 0 && intLike == nonEmpty)
            {
                return column;
            }
        }

        return null;
    }

    private static int NormalizeId(string idValue)
    {
        return int.TryParse(idValue, out var id) && id >= 0 ? id : 0;
    }

    private static bool IsValidDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 10) return false;
        // very naive YYYY-MM-DD
        return char.IsDigit(value[0]) && char.IsDigit(value[1]) && char.IsDigit(value[2]) && char.IsDigit(value[3]) &&
               value[4] == '-' &&
               char.IsDigit(value[5]) && char.IsDigit(value[6]) &&
               value[7] == '-' &&
               char.IsDigit(value[8]) && char.IsDigit(value[9]);
    }

    private async Task DebouncedRefreshByIdAsync()
    {
        _idFilterDebounceCts?.Cancel();
        _idFilterDebounceCts?.Dispose();
        _idFilterDebounceCts = new CancellationTokenSource();
        var token = _idFilterDebounceCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (!token.IsCancellationRequested)
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
