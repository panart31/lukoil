namespace Lukoil.Client.Models;

public sealed class NavigationItem
{
    public string Title { get; init; } = string.Empty;
    public object ViewModel { get; init; } = new();
}
