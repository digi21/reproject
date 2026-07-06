using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Reproject;

public partial class App : Application
{
    // The composition root. Views resolve their view models from here.
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Platform / infrastructure services
        services.AddSingleton<ILocalizer, ResourceLocalizer>();
        services.AddSingleton<IActiveWindow, ActiveWindow>();
        services.AddSingleton<ICrsService, CrsService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IFavoritesStore, JsonFavoritesStore>();
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IDialogService, DialogService>();

        // View models
        services.AddSingleton<MainViewModel>();
        services.AddTransient<EpsgPickerViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Refresh the grid-download source list from the public crskit repo in the background, so the
        // transformation picker can offer up-to-date download links. Fire-and-forget: the bundled
        // default (or the cached copy) is used until this completes, and any failure is ignored.
        _ = GridDownloadCatalog.InitializeAsync();

        _window = new MainWindow();
        _window.Activate();
    }
}
