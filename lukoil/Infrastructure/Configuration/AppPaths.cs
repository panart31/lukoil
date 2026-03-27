using System;
using System.IO;

namespace Lukoil.Client.Config;

public static class AppPaths
{
    public static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LukoilClient");
    public static string SettingsPath => Path.Combine(ConfigDirectory, "settings.json");
    public static string LogDirectory => Path.Combine(ConfigDirectory, "logs");
    public static string AppLogPath => Path.Combine(LogDirectory, "app-.log");
}
