using System.IO;
using System.Text.Json;
using Lukoil.Client.Config;
using Lukoil.Client.Models;

namespace Lukoil.Client.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<ConnectionSettings> LoadConnectionSettingsAsync()
    {
        if (!File.Exists(AppPaths.SettingsPath))
        {
            return new ConnectionSettings();
        }

        await using var stream = File.OpenRead(AppPaths.SettingsPath);
        var model = await JsonSerializer.DeserializeAsync<ConnectionSettings>(stream, JsonOptions);
        return model ?? new ConnectionSettings();
    }

    public async Task SaveConnectionSettingsAsync(ConnectionSettings settings)
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        await using var stream = File.Create(AppPaths.SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
