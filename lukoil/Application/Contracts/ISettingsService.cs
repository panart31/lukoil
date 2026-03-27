using Lukoil.Client.Models;

namespace Lukoil.Client.Services;

public interface ISettingsService
{
    Task<ConnectionSettings> LoadConnectionSettingsAsync();
    Task SaveConnectionSettingsAsync(ConnectionSettings settings);
}
