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

        // The restored pair is only transformed once the content is loaded: building it may need to ask
        // the user something (a missing grid file to download), and a dialog needs a live XamlRoot.
        if (Content is FrameworkElement root)
            root.Loaded += (_, _) => _ = VM.StartAsync();
    }
}
