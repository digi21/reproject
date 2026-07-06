using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Reproject;

// Drives the Settings dialog. For now it configures the geoid/grid models directory: where the
// app looks for geoid model files (e.g. the global EGM2008 grid, which is not shipped with the
// app). "Open in Explorer" opens that folder so the user can drop a grid file into it.
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IFilePickerService _files;

    [ObservableProperty] public partial string ModelsDirectory { get; set; }

    public SettingsViewModel(ISettingsStore settings, IFilePickerService files, ICrsService crs)
    {
        _settings = settings;
        _files = files;
        // Show the configured folder, or the current effective default when none is set yet.
        ModelsDirectory = _settings.LoadGeoidModelsDirectory() ?? crs.GetGeoidModelsDirectory();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _files.PickFolderAsync();
        if (picked is not null) ModelsDirectory = picked;
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        var dir = ModelsDirectory?.Trim();
        if (string.IsNullOrEmpty(dir)) return;
        try
        {
            Directory.CreateDirectory(dir);   // create it if new, so the user can paste a file into it
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch { /* opening the folder is best-effort */ }
    }

    // Persist the chosen directory; it takes effect on the next launch (the engine binds its data
    // directory once, at startup).
    public void Save() =>
        _settings.SaveGeoidModelsDirectory(string.IsNullOrWhiteSpace(ModelsDirectory) ? null : ModelsDirectory.Trim());
}
