using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Reproject;

// Gives services access to the current window's HWND (file pickers) and XamlRoot
// (content dialogs) without the view models knowing about windows.
public interface IActiveWindow
{
    nint Hwnd { get; }
    XamlRoot? XamlRoot { get; }
    void Attach(Window window);
}

public sealed class ActiveWindow : IActiveWindow
{
    private Window? _window;

    public void Attach(Window window) => _window = window;

    public nint Hwnd => _window is null ? 0 : WindowNative.GetWindowHandle(_window);

    public XamlRoot? XamlRoot => _window?.Content?.XamlRoot;
}
