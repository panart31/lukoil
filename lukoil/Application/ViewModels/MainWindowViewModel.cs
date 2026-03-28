using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lukoil.Client.Models;
using Lukoil.Client.Parsers;
using Lukoil.Client.Services;

namespace Lukoil.Client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int DefaultPort = 55000;
    private const int DefaultTimeoutMs = 5000;
    private readonly ITcpClientService _tcpClient;
    private readonly IResponseParser _parser;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly List<DataScreenViewModel> _dataScreens = [];

    private string _lastCommand = "GET_STATS";

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = DefaultPort;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Отключено";

    [ObservableProperty]
    private object? _currentScreen;

    [ObservableProperty]
    private NavigationItem? _selectedNavigation;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    public DashboardViewModel Dashboard { get; }
    public ComboQueryViewModel ComboQuery { get; }

    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand RefreshCurrentCommand { get; }

    public MainWindowViewModel(ITcpClientService tcpClient, IResponseParser parser, ISettingsService settings, ILogService log)
    {
        _tcpClient = tcpClient;
        _parser = parser;
        _settings = settings;
        _log = log;

        Dashboard = new DashboardViewModel(ExecuteCommandAsync, OpenScreenForCommandAsync);
        ComboQuery = new ComboQueryViewModel(ExecuteCommandAsync, NavigateHomeAsync);

        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        RefreshCurrentCommand = new AsyncRelayCommand(RefreshCurrentAsync);

        BuildNavigation();
        LoadSettingsAsync();
    }

    partial void OnSelectedNavigationChanged(NavigationItem? value)
    {
        CurrentScreen = value?.ViewModel;
        _ = RefreshSelectedScreenAsync();
    }

    private void BuildNavigation()
    {
        var products = new DataScreenViewModel("Товары", "GET_ALL_PRODUCTS", ExecuteCommandAsync, NavigateHomeAsync);
        var inventory = new DataScreenViewModel("Склад", "GET_INVENTORY", ExecuteCommandAsync, NavigateHomeAsync);
        var invoices = new DataScreenViewModel("Счета‑фактуры", "GET_INVOICES", ExecuteCommandAsync, NavigateHomeAsync, supportsIdFilter: true);
        var shipments = new DataScreenViewModel("Отгрузки", "GET_SHIPMENTS", ExecuteCommandAsync, NavigateHomeAsync, supportsIdFilter: true, supportsDateRange: true);
        var clientDebt = new DataScreenViewModel("Задолженности клиентов", "GET_CLIENT_DEBT", ExecuteCommandAsync, NavigateHomeAsync, supportsIdFilter: true);
        // Удалены по требованию: статистика/выручка/просрочка/обороты/комбо

        _dataScreens.AddRange([products, inventory, invoices, shipments, clientDebt]);

        NavigationItems.Add(new NavigationItem { Title = "Главная", ViewModel = Dashboard });
        NavigationItems.Add(new NavigationItem { Title = "Товары", ViewModel = products });
        NavigationItems.Add(new NavigationItem { Title = "Склад", ViewModel = inventory });
        NavigationItems.Add(new NavigationItem { Title = "Счета‑фактуры", ViewModel = invoices });
        NavigationItems.Add(new NavigationItem { Title = "Отгрузки", ViewModel = shipments });
        NavigationItems.Add(new NavigationItem { Title = "Задолженности", ViewModel = clientDebt });
        // Удалены по требованию вкладки: Статистика/Выручка/Просрочка/Обороты/Комбо

        SelectedNavigation = NavigationItems.First();
    }

    private async void LoadSettingsAsync()
    {
        var settings = await _settings.LoadConnectionSettingsAsync();
        Host = settings.Host;
        Port = settings.Port <= 0 || settings.Port == 5555 || settings.Port == 5500 ? DefaultPort : settings.Port;

        if (Port == DefaultPort && settings.Port != DefaultPort)
        {
            await SaveSettingsAsync();
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            IsBusy = true;
            await _tcpClient.ConnectAsync(Host, Port, DefaultTimeoutMs);
            IsConnected = true;
            StatusText = $"Подключено: {Host}:{Port}";
            _log.Info(StatusText);
            await SaveSettingsAsync();
            await RefreshAllDataScreensAsync();
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = "Ошибка подключения";
            _log.Error("Не удалось подключиться", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        await _settings.SaveConnectionSettingsAsync(new ConnectionSettings
        {
            Host = Host,
            Port = Port
        });
    }

    private async Task RefreshCurrentAsync()
    {
        if (CurrentScreen is DataScreenViewModel tableScreen)
        {
            await tableScreen.RefreshAsync();
            return;
        }
    }

    private async Task RefreshSelectedScreenAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        if (CurrentScreen is DataScreenViewModel tableScreen)
        {
            await tableScreen.RefreshAsync();
            return;
        }
    }

    private async Task RefreshAllDataScreensAsync()
    {
        foreach (var screen in _dataScreens)
        {
            await screen.RefreshAsync();
        }
    }

    private async Task OpenScreenForCommandAsync(string command)
    {
        var normalized = command.Split('|')[0];
        var target = _dataScreens.FirstOrDefault(s => s.Command == normalized);
        if (target is null)
        {
            await ExecuteCommandAsync(command);
            return;
        }

        var navItem = NavigationItems.FirstOrDefault(n => ReferenceEquals(n.ViewModel, target));
        if (navItem is not null)
        {
            SelectedNavigation = navItem;
        }

        if (target.SupportsIdFilter && command.Contains('|'))
        {
            target.IdFilter = command.Split('|')[1];
        }

        await target.RefreshAsync();
    }

    private Task NavigateHomeAsync()
    {
        var home = NavigationItems.FirstOrDefault(item => item.Title == "Главная");
        if (home is not null)
        {
            SelectedNavigation = home;
        }

        return Task.CompletedTask;
    }

    public async Task<QueryResult> ExecuteCommandAsync(string command)
    {
        if (!IsConnected)
        {
            return QueryResult.Fail("Нет активного подключения. Сначала подключитесь к серверу.");
        }

        _lastCommand = command;

        try
        {
            IsBusy = true;
            _log.Info($"> {command}");
            var raw = await _tcpClient.SendCommandAsync(command, DefaultTimeoutMs);
            var rawTrim = raw.Trim();

            // Явное детектирование ошибок сервера/БД в текстовом формате
            if (rawTrim.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase) ||
                rawTrim.StartsWith("FATAL", StringComparison.OrdinalIgnoreCase) ||
                rawTrim.Contains("\nERROR|", StringComparison.OrdinalIgnoreCase))
            {
                _log.Error("Ответ сервера с ошибкой", null);
                return QueryResult.Fail(rawTrim);
            }

            var parsed = _parser.ParseTable(raw);
            _log.Info($"Получен ответ ({raw.Length} символов)");
            return QueryResult.Success(raw, parsed);
        }
        catch (TimeoutException ex)
        {
            _log.Error("Таймаут запроса", ex);
            return QueryResult.Fail("Запрос превысил таймаут.");
        }
        catch (IOException ex)
        {
            _log.Error("Сетевая ошибка", ex);
            return QueryResult.Fail("Сетевая ошибка (разрыв соединения / broken pipe).");
        }
        catch (Exception ex)
        {
            _log.Error("Непредвиденная ошибка команды", ex);
            return QueryResult.Fail($"Ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
