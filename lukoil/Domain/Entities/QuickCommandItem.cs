using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lukoil.Client.Models;

public partial class QuickCommandItem : ObservableObject
{
    public string Title { get; init; } = string.Empty;
    public string CommandText { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public IAsyncRelayCommand ExecuteCommand { get; init; } = null!;
}
