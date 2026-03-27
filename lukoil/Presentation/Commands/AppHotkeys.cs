using System.Windows.Input;

namespace Lukoil.Client.Commands;

public static class AppHotkeys
{
    public static readonly KeyGesture SendGesture = new(Key.Enter, ModifierKeys.Control);
    public static readonly KeyGesture RefreshGesture = new(Key.F5);
}
