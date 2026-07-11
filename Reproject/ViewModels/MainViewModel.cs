using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace Reproject;

public partial class MainViewModel : ObservableObject
{
    private readonly ICrsService _crs;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _files;
    private readonly ILocalizer _localizer;

    // Debounces re-transforming while the user types (created on the UI thread).
    private readonly DispatcherQueueTimer? _debounce;
    private ITransformation? _transformation;

    // Partial properties (not fields) so the CsWinRT/MVVM source generators emit
    // WinRT-friendly, AOT-safe code for x:Bind (see MVVMTK0045).
    [ObservableProperty] public partial CrsSelection? SourceSelection { get; set; }
    [ObservableProperty] public partial CrsSelection? TargetSelection { get; set; }
    [ObservableProperty] public partial string SourceCrsName { get; set; }
    [ObservableProperty] public partial string TargetCrsName { get; set; }
    [ObservableProperty] public partial string SourceAxes { get; set; }
    [ObservableProperty] public partial string TargetAxes { get; set; }
    [ObservableProperty] public partial string InputText { get; set; }
    [ObservableProperty] public partial string OutputText { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }

    // Errors go to a dockable, closable banner (InfoBar) instead of the status bar, so long
    // messages (e.g. a missing geoid grid and its searched path) are shown in full.
    [ObservableProperty] public partial string ErrorMessage { get; set; }
    [ObservableProperty] public partial bool IsErrorOpen { get; set; }

    // Recently-used systems, most-recent first, shared by the source and target dropdowns.
    public ObservableCollection<CrsSelection> RecentSystems { get; } = new();
    private const int MaxRecent = 12;

    // Set while restoring at startup so assigning Source/TargetSelection does not re-apply (rebuild or
    // show dialogs); StartAsync performs a single rebuild once the UI can show them.
    private bool _suppressApply;

    // Guards StartAsync against a second Loaded (the first build must not run twice).
    private bool _started;

    // The EPSG operation the user picked for the current pair, remembered across runs: an ambiguous pair
    // (say WGS 84 -> EGM2008 height, where the geoid comes at several grid resolutions) must come back
    // transforming the way they chose, not silently switch to another operation. Null when they never
    // chose — the pair is unambiguous, or the choice belongs to a pair they have since changed.
    private int? _operationCode;

    public MainViewModel(ICrsService crs, ISettingsStore settings, IDialogService dialogs,
        IFilePickerService files, ILocalizer localizer)
    {
        _crs = crs;
        _settings = settings;
        _dialogs = dialogs;
        _files = files;
        _localizer = localizer;

        SourceCrsName = _localizer.Get("NoneSelected");
        TargetCrsName = _localizer.Get("NoneSelected");
        SourceAxes = string.Empty;
        TargetAxes = string.Empty;
        InputText = string.Empty;
        OutputText = string.Empty;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher is not null)
        {
            _debounce = dispatcher.CreateTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(150);
            _debounce.IsRepeating = false;
            _debounce.Tick += (_, _) => TransformNow();
        }
    }

    // Startup: bring up the engine and restore the last-used CRS. Call once, on the UI thread.
    // Only restores the selections; building their transformation waits for StartAsync.
    public void Initialize()
    {
        StatusMessage = _crs.EnsureInitialized();
        RestoreLastSelections();
    }

    // Build the restored pair's transformation. Called once the window is shown, not from Initialize:
    // until then there is no XamlRoot, and a restored pair whose grid file is missing needs to offer
    // the download dialog like any other selection would. Nothing above catches what this throws (it is
    // started, not awaited), so a failure has to end up in the banner here or it is lost silently.
    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;

        if (SourceSelection is null || TargetSelection is null) return;

        try { await RebuildTransformationAsync(letUserChooseOperation: false); }
        catch (Exception ex) { ShowTransformError(ex); }
    }

    [RelayCommand]
    private async Task PickSourceAsync()
    {
        var crs = await _dialogs.PickCrsAsync(SourceSelection);
        if (crs is null) return;
        AddToRecent(crs);
        SourceSelection = crs;   // now in RecentSystems; OnSourceSelectionChanged applies it
    }

    [RelayCommand]
    private async Task PickTargetAsync()
    {
        var crs = await _dialogs.PickCrsAsync(TargetSelection);
        if (crs is null) return;
        AddToRecent(crs);
        TargetSelection = crs;   // now in RecentSystems; OnTargetSelectionChanged applies it
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var text = await _files.OpenTextAsync();
        if (text is not null) InputText = text;
    }

    [RelayCommand]
    private Task ExportAsync() => _files.SaveTextAsync(OutputText, _localizer.Get("ExportFileName"));

    [RelayCommand]
    private Task ShowAboutAsync() => _dialogs.ShowAboutAsync();

    [RelayCommand]
    private Task ShowSettingsAsync() => _dialogs.ShowSettingsAsync();

    // Choosing a system (from the dropdown or the picker) applies it: refresh the labels, rebuild the
    // transformation and persist. Skipped while restoring at startup (see _suppressApply).
    partial void OnSourceSelectionChanged(CrsSelection? value)
    {
        if (!_suppressApply && value is not null) _ = ApplySourceAsync(value);
    }

    partial void OnTargetSelectionChanged(CrsSelection? value)
    {
        if (!_suppressApply && value is not null) _ = ApplyTargetAsync(value);
    }

    private async Task ApplySourceAsync(CrsSelection selection)
    {
        SourceCrsName = selection.DisplayName;
        SourceAxes = DescribeAxes(selection.Wkt);
        _operationCode = null;   // the remembered operation was for the pair being replaced
        await RebuildTransformationAsync(letUserChooseOperation: true);
        Persist();
    }

    private async Task ApplyTargetAsync(CrsSelection selection)
    {
        TargetCrsName = selection.DisplayName;
        TargetAxes = DescribeAxes(selection.Wkt);
        _operationCode = null;   // the remembered operation was for the pair being replaced
        await RebuildTransformationAsync(letUserChooseOperation: true);
        Persist();
    }

    // Add a system to the front of the recent list, de-duplicated by WKT and capped at MaxRecent.
    private void AddToRecent(CrsSelection selection)
    {
        for (var i = RecentSystems.Count - 1; i >= 0; i--)
            if (RecentSystems[i].Wkt == selection.Wkt) RecentSystems.RemoveAt(i);
        RecentSystems.Insert(0, selection);
        while (RecentSystems.Count > MaxRecent) RecentSystems.RemoveAt(RecentSystems.Count - 1);
    }

    private void Persist() => _settings.Save(SourceSelection, TargetSelection, RecentSystems, _operationCode);

    private void RestoreLastSelections()
    {
        var (source, target, recent, operationCode) = _settings.Load();
        foreach (var r in recent) RecentSystems.Add(r);
        _operationCode = operationCode;

        _suppressApply = true;   // assign the selections without re-applying; one silent rebuild below
        try
        {
            if (source is not null)
            {
                var s = ResolveRecent(source);
                SourceSelection = s;
                SourceCrsName = s.DisplayName;
                SourceAxes = DescribeAxes(s.Wkt);
            }
            if (target is not null)
            {
                var t = ResolveRecent(target);
                TargetSelection = t;
                TargetCrsName = t.DisplayName;
                TargetAxes = DescribeAxes(t.Wkt);
            }
        }
        finally { _suppressApply = false; }
    }

    // The recent-list instance matching selection (so the dropdown shows it selected), adding it if
    // absent. Matching by WKT keeps the same system single in the list.
    private CrsSelection ResolveRecent(CrsSelection selection)
    {
        var item = RecentSystems.FirstOrDefault(r => r.Wkt == selection.Wkt);
        if (item is null) { RecentSystems.Insert(0, selection); item = selection; }
        return item;
    }

    // Generated hook: re-transform (debounced) whenever the input changes.
    partial void OnInputTextChanged(string value)
    {
        if (_debounce is null) { TransformNow(); return; }
        _debounce.Stop();
        _debounce.Start();
    }

    private string DescribeAxes(string wkt)
    {
        try { return _crs.DescribeAxes(wkt); }
        catch { return string.Empty; }
    }

    // letUserChooseOperation: true when the user picked a system, so an ambiguous pair is theirs to
    // disambiguate. false at startup restore, where being asked to choose again on every launch would
    // be noise — there we silently take the most accurate operation. Either way a missing grid file is
    // offered for download: by now the window is loaded, so dialogs can be shown.
    private async Task RebuildTransformationAsync(bool letUserChooseOperation)
    {
        if (SourceSelection is null || TargetSelection is null)
        {
            _transformation = null;
            return;
        }

        var source = SourceSelection.Wkt;
        var target = TargetSelection.Wkt;

        // The operation the user chose for this pair, if any; otherwise let the engine pick.
        ITransformation Build() => _operationCode is int code
            ? _crs.CreateTransformationFromWkt(source, target, code)
            : _crs.CreateTransformationFromWkt(source, target);

        try
        {
            ApplyTransformation(Build());
        }
        catch (Exception ex)
        {
            var error = ex;

            // If the build failed only because a grid file is missing and we can provide it, offer to
            // download it (or say where to put it) and retry — this covers the single-operation case and
            // the remembered one, which never reach the picker below. Loop in case more than one grid is
            // needed.
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var missing = ExtractMissingGridFile(error.Message);
                if (missing is null || !await EnsureGridFileAsync(missing)) break;
                try { ApplyTransformation(Build()); return; }
                catch (Exception retryEx) { error = retryEx; }
            }

            // A remembered operation that no longer builds (EPSG data changed, say) must not wedge the
            // app: forget it and fall through to the candidates below.
            _operationCode = null;

            // The failure may just be "several operations exist, none chosen". Ask the engine
            // for the candidates: if there really are several, let the user pick one; otherwise
            // it is a genuine error and we surface it in the banner.
            IReadOnlyList<TransformationOption> candidates;
            try { candidates = _crs.GetCandidateOperations(source, target); }
            catch { candidates = []; }

            if (candidates.Count <= 1)
            {
                ShowTransformError(error);
                return;
            }

            TransformationOption selected;
            if (letUserChooseOperation)
            {
                TransformationOption? chosen;
                try { chosen = await _dialogs.PickTransformationAsync(candidates); }
                catch (Exception dialogEx) { ShowTransformError(dialogEx); return; }

                if (chosen is null)
                {
                    // Cancelled: leave no transformation, but this is not an error.
                    _transformation = null;
                    OutputText = string.Empty;
                    StatusMessage = string.Empty;
                    IsErrorOpen = false;
                    return;
                }
                selected = chosen;
                _operationCode = selected.Code;   // remember it, for this session and the next
            }
            else
            {
                // Startup with nothing remembered: take the most accurate, but do not record it as a
                // choice — it is a default, and the user should still be asked when they change a system.
                selected = MostAccurate(candidates);
            }

            await EnsureGridsAsync(selected);
            try { ApplyTransformation(_crs.CreateTransformationFromWkt(source, target, selected.Code)); }
            catch (Exception ex2) { ShowTransformError(ex2); }
        }
    }

    // Most accurate operation: smallest stated accuracy, unknown accuracy last. Same order the
    // selection dialog lists them, so the startup default matches its top row.
    private static TransformationOption MostAccurate(IReadOnlyList<TransformationOption> candidates) =>
        candidates
            .OrderBy(o => o.Accuracy.HasValue ? 0 : 1)
            .ThenBy(o => o.Accuracy ?? double.MaxValue)
            .First();

    // Before building an operation, make sure the grid file(s) it needs are present in the geoid folder.
    // For a missing file we know how to fetch directly, ask the user (showing the size) and download it
    // with a progress bar. Anything we cannot fetch (page-only or unknown) is left for the build to
    // report as missing.
    private async Task EnsureGridsAsync(TransformationOption op)
    {
        if (string.IsNullOrEmpty(op.GridFiles)) return;
        foreach (var file in op.GridFiles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            await EnsureGridFileAsync(file);
    }

    // Make one grid file available in the geoid folder: if it is already there, done; if we can fetch it
    // directly, confirm the size with the user and download it with a progress bar; if we only know a
    // page for it, tell the user where to get it and where to put it. Returns true if it is present after.
    private async Task<bool> EnsureGridFileAsync(string file)
    {
        var folder = _crs.GetGeoidModelsDirectory();
        if (string.IsNullOrEmpty(folder)) return false;

        // Install under the canonical (uncompressed) name the engine looks for, regardless of whether
        // the EPSG name carried a ".gz" suffix.
        file = GridDownloadCatalog.NormalizeName(file.Trim());
        var destination = Path.Combine(folder, file);
        if (File.Exists(destination)) return true;

        var url = GridDownloadCatalog.DirectUrlFor(file);
        if (url is null)
        {
            var source = GridDownloadCatalog.Find(file);
            if (source is not null)
                await _dialogs.ShowGridInstructionsAsync(file, source.Url, folder);
            return false;
        }

        var size = await GridDownloadCatalog.ProbeSizeAsync(url);
        await _dialogs.DownloadGridAsync(file, size, progress => GridDownloadCatalog.DownloadFileAsync(url, destination, progress));
        return File.Exists(destination);
    }

    // Extract the grid file name from a crskit "Grid file '<name>' ... not found" message, or null.
    private static string? ExtractMissingGridFile(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message, "[Gg]rid file '([^']+)'");
        return match.Success ? match.Groups[1].Value : null;
    }

    private void ApplyTransformation(ITransformation transformation)
    {
        _transformation = transformation;
        IsErrorOpen = false;
        StatusMessage = transformation.IsIdentity
            ? _localizer.Get("StatusIdentity")
            : $"{transformation.SourceName} → {transformation.TargetName}";
        TransformNow();
    }

    private void ShowTransformError(Exception ex)
    {
        _transformation = null;
        OutputText = string.Empty;
        StatusMessage = string.Empty;
        ErrorMessage = _localizer.Format("StatusTransformError", ex.Message);
        IsErrorOpen = true;
    }

    /// <summary>
    /// Transforms every non-empty input line, preserving line positions. One bad
    /// line does not abort the batch; it is reported inline in the output.
    /// </summary>
    public void TransformNow()
    {
        if (_transformation is null)
        {
            OutputText = string.Empty;
            return;
        }

        var dimension = _transformation.SourceDimension;
        var separators = new[] { ' ', ',', '\t', ';' };
        var builder = new StringBuilder();
        var okLines = 0;
        var badLines = 0;

        foreach (var rawLine in InputText.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (line.Length == 0)
            {
                builder.Append('\n');
                continue;
            }

            var parts = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < dimension)
            {
                builder.Append(_localizer.Format("ErrExpectedValues", dimension) + "\n");
                badLines++;
                continue;
            }

            var point = new double[dimension];
            var parsed = true;
            for (var i = 0; i < dimension; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out point[i]))
                {
                    parsed = false;
                    break;
                }
            }

            if (!parsed)
            {
                builder.Append(_localizer.Get("ErrInvalidNumber") + "\n");
                badLines++;
                continue;
            }

            try
            {
                var result = _transformation.Transform(point);
                for (var i = 0; i < result.Length; i++)
                {
                    if (i > 0) builder.Append(' ');
                    builder.Append(result[i].ToString("0.############", CultureInfo.InvariantCulture));
                }
                builder.Append('\n');
                okLines++;
            }
            catch (Exception ex)
            {
                builder.Append(_localizer.Format("ErrLine", ex.Message) + "\n");
                badLines++;
            }
        }

        OutputText = builder.ToString().TrimEnd('\n');
        StatusMessage = badLines > 0
            ? _localizer.Format("StatusTransformedErrors", okLines, badLines)
            : _localizer.Format("StatusTransformed", okLines);
    }
}
