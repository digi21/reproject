using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Reproject;

// Opens and saves coordinate text files and shows a picker to select the geoid models directory. Pickers
// need the owning window HWND in an unpackaged app (InitializeWithWindow).
public interface IFilePickerService
{
    Task<string?> OpenTextAsync();
    Task SaveTextAsync(string content, string suggestedName);
    // Pick a folder (for the geoid models directory). Returns its path, or null if cancelled.
    Task<string?> PickFolderAsync();
}

public sealed class FilePickerService(IActiveWindow window, ILocalizer localizer) : IFilePickerService
{
    public async Task<string?> OpenTextAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, window.Hwnd);
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        return file is null ? null : await FileIO.ReadTextAsync(file);
    }

    public async Task SaveTextAsync(string content, string suggestedName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, window.Hwnd);
        picker.FileTypeChoices.Add(localizer.Get("FileTypeText"), new List<string> { ".txt" });
        picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
        picker.SuggestedFileName = suggestedName;

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await FileIO.WriteTextAsync(file, content);
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, window.Hwnd);
        picker.FileTypeFilter.Add("*");   // FolderPicker requires at least one filter to work
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
