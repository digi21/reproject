using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Reproject;

public sealed partial class MainWindow : Window
{
    public MainViewModel VM { get; }

    public MainWindow()
    {
        App.Services.GetRequiredService<IActiveWindow>().Attach(this);
        VM = App.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();

        // Remove the system title bar: extend the content up and use the app's own
        // title strip as the draggable region (the caption buttons stay).
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "reproject.ico"));

        VM.Initialize();
    }
}
