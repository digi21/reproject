using System;
using System.IO;
using System.Threading.Tasks;
using Digi21.WinUI.CrsPicker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.System;

namespace Reproject;

// Shows the modal dialogs of the application: the CRS picker of the Digi21.WinUI.CrsPicker package, the about,
// operation, settings and grid download dialogs.
public interface IDialogService
{
    // current: the system currently selected for this slot, so the picker can reopen on the same
    // option with its choices already filled in. null when nothing is selected yet.
    Task<CrsSelection?> PickCrsAsync(CrsSelection? current = null);
    Task ShowAboutAsync();
    // Ask the user which of several candidate EPSG operations to use. Returns the chosen
    // one, or null if cancelled.
    Task<TransformationOption?> PickTransformationAsync(System.Collections.Generic.IReadOnlyList<TransformationOption> candidates);
    Task ShowSettingsAsync();
    // Ask the user to confirm downloading a grid file (showing its size) and, if accepted, run the
    // download while showing a progress bar. Returns true if it downloaded successfully.
    Task<bool> DownloadGridAsync(string fileName, long? sizeBytes, Func<IProgress<double>, Task> download);
    // Tell the user that a grid file they need is not auto-downloadable: name it, say which folder to
    // put it in, and offer to open the download page and that folder.
    Task ShowGridInstructionsAsync(string fileName, string pageUrl, string folder);
}

public sealed class DialogService(IActiveWindow window, IServiceProvider services) : IDialogService
{
    // The CRS picker keeps the favorite systems in %LOCALAPPDATA%\Reproject\favorites.json.
    private static readonly string FavoritesFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Reproject", "favorites.json");

    public Task<CrsSelection?> PickCrsAsync(CrsSelection? current = null) =>
        CrsPicker.PickAsync(
            window.XamlRoot ?? throw new InvalidOperationException("The main window has no XamlRoot."),
            new CrsPickerOptions { FavoritesFilePath = FavoritesFilePath, InitialSelection = current });

    public async Task ShowAboutAsync()
    {
        var version = services.GetRequiredService<ICrsService>().GetEpsgVersion();
        var dialog = new AboutDialog(version) { XamlRoot = window.XamlRoot };
        await dialog.ShowAsync();
    }

    public async Task<TransformationOption?> PickTransformationAsync(
        System.Collections.Generic.IReadOnlyList<TransformationOption> candidates)
    {
        var vm = new OperationPickerViewModel(candidates, services.GetRequiredService<ILocalizer>());
        var dialog = new OperationPickerDialog(vm) { XamlRoot = window.XamlRoot };
        await dialog.ShowAsync();
        return vm.Result;
    }

    public async Task ShowSettingsAsync()
    {
        var vm = services.GetRequiredService<SettingsViewModel>();
        var dialog = new SettingsDialog(vm) { XamlRoot = window.XamlRoot };
        await dialog.ShowAsync();
    }

    public async Task<bool> DownloadGridAsync(string fileName, long? sizeBytes, Func<IProgress<double>, Task> download)
    {
        var loc = services.GetRequiredService<ILocalizer>();
        var sizeText = sizeBytes is long b && b > 0
            ? $"{b / (1024.0 * 1024.0):0.#} MB"
            : loc.Get("DownloadSizeUnknown");

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = loc.Format("DownloadMessage", fileName, sizeText),
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(bar);

        var dialog = new ContentDialog
        {
            XamlRoot = window.XamlRoot,
            Title = loc.Get("DownloadTitle"),
            Content = panel,
            PrimaryButtonText = loc.Get("DownloadProceed"),
            CloseButtonText = loc.Get("DownloadCancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var ok = false;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            // Keep the dialog open while downloading: take a deferral, switch it into progress mode, and
            // complete the deferral (which closes it) only when the download finishes.
            var deferral = args.GetDeferral();
            dialog.IsPrimaryButtonEnabled = false;
            dialog.CloseButtonText = string.Empty;   // no cancel mid-download (kept simple)
            bar.Visibility = Visibility.Visible;
            var progress = new Progress<double>(p => bar.Value = Math.Clamp(p * 100.0, 0, 100));
            try { await download(progress); ok = true; }
            catch { ok = false; }
            deferral.Complete();
        };

        await dialog.ShowAsync();
        return ok;
    }

    public async Task ShowGridInstructionsAsync(string fileName, string pageUrl, string folder)
    {
        var loc = services.GetRequiredService<ILocalizer>();
        var dialog = new ContentDialog
        {
            XamlRoot = window.XamlRoot,
            Title = loc.Get("GridNeededTitle"),
            Content = new TextBlock
            {
                Text = loc.Format("GridNeededMessage", fileName, folder),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = loc.Get("GridOpenPage"),
            SecondaryButtonText = loc.Get("GridOpenFolder"),
            CloseButtonText = loc.Get("GridClose"),
            DefaultButton = ContentDialogButton.Primary,
        };

        // Opening the page / folder should not dismiss the dialog, so the user can do both then close.
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            try { if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri)) await Launcher.LaunchUriAsync(uri); }
            catch { /* nothing we can do if the shell refuses */ }
        };
        dialog.SecondaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            try { await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(folder)); }
            catch { /* folder may not exist yet; ignore */ }
        };

        await dialog.ShowAsync();
    }
}
