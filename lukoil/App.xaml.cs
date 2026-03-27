using System.IO;
using System.Windows;
using Lukoil.Client.Config;
using Lukoil.Client.Parsers;
using Lukoil.Client.Services;
using Lukoil.Client.ViewModels;

namespace Lukoil.Client;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        Directory.CreateDirectory(AppPaths.LogDirectory);

        var logService = new LogService();
        var settingsService = new SettingsService();
        var parser = new ResponseParser(logService);
        var tcpClientService = new TcpClientService(logService);

        var vm = new MainWindowViewModel(tcpClientService, parser, settingsService, logService);
        var window = new MainWindow
        {
            DataContext = vm
        };
        window.Show();
    }
}
