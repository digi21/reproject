using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Reproject;

// File open/save for the coordinate text and for ESRI-style .prj (WKT) files. Pickers
// need the owning window HWND in an unpackaged app (InitializeWithWindow).
public interface IFilePickerService
{
    Task<string?> OpenTextAsync();
    Task SaveTextAsync(string content, string suggestedName);
    Task<string?> ImportPrjAsync();
    Task ExportPrjAsync(string wkt, string suggestedName);
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

    public async Task<string?> ImportPrjAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, window.Hwnd);
        picker.FileTypeFilter.Add(".prj");
        picker.FileTypeFilter.Add(".wkt");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        return (await FileIO.ReadTextAsync(file)).Trim();
    }

    public async Task ExportPrjAsync(string wkt, string suggestedName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, window.Hwnd);
        picker.FileTypeChoices.Add(localizer.Get("FileTypePrj"), new List<string> { ".prj" });
        picker.FileTypeChoices.Add("WKT", new List<string> { ".wkt" });
        picker.SuggestedFileName = suggestedName;

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        await FileIO.WriteTextAsync(file, wkt);
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
