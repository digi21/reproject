using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Reproject;

public partial class EpsgPickerViewModel : ObservableObject
{
    // LabelKey is a Resources.resw key (localized when building Categories).
    private sealed record Category(string LabelKey, string Mode, string? Kind);

    private static readonly Category[] CategoryDefs =
    {
        new("CatGeographic2D", "list", "geographic 2D"),
        new("CatGeographic3D", "list", "geographic 3D"),
        new("CatProjected",    "list", "projected"),
        new("CatVertical",     "list", "vertical"),
        new("CatCompound",     "compound", null),
        new("CatManual",       "manual", null),
        new("CatFavorites",    "favorites", null),
    };

    private readonly ICrsService _crs;
    private readonly IFavoritesStore _favorites;
    private readonly IFilePickerService _files;
    private readonly IClipboardService _clipboard;
    private readonly ILocalizer _localizer;

    private string _mode = "list";
    private string? _kind;
    private List<CrsRow>? _listRows;     // rows of the current list kind (cached for the suggest box)
    private CrsRow? _selectedListRow;    // the CRS chosen in list mode
    private List<CrsRow>? _mergedHorizontal;
    private CrsRow? _compoundH;
    private CrsRow? _compoundV;
    private string _candidateName = string.Empty;
    private string _candidateWkt = string.Empty;

    public EpsgPickerViewModel(ICrsService crs, IFavoritesStore favorites,
        IFilePickerService files, IClipboardService clipboard, ILocalizer localizer)
    {
        _crs = crs;
        _favorites = favorites;
        _files = files;
        _clipboard = clipboard;
        _localizer = localizer;

        Categories = CategoryDefs.Select(c => _localizer.Get(c.LabelKey)).ToList();

        ListText = string.Empty;
        ManualWkt = string.Empty;
        WktPreview = string.Empty;
        HorizontalText = string.Empty;
        VerticalText = string.Empty;
        StandardAxisLabel = _localizer.Get("AxisStandard");
        DetailName = _localizer.Get("DetailNoSelection");
        DetailMeta = string.Empty;
        DetailDatum = string.Empty;

        ApplyCategory(0);
    }

    // ---- bound collections / result -----------------------------------------

    public IReadOnlyList<string> Categories { get; }
    public ObservableCollection<CrsRow> ListSuggestions { get; } = new();
    public ObservableCollection<CrsRow> HorizontalSuggestions { get; } = new();
    public ObservableCollection<CrsRow> VerticalSuggestions { get; } = new();
    public ObservableCollection<CrsSelection> Favorites { get; } = new();

    // The chosen CRS once the user confirms, or null.
    public CrsSelection? Result { get; private set; }

    // ---- bound state ---------------------------------------------------------

    [ObservableProperty] public partial int SelectedCategoryIndex { get; set; }
    [ObservableProperty] public partial bool IsListMode { get; set; }
    [ObservableProperty] public partial bool IsCompoundMode { get; set; }
    [ObservableProperty] public partial bool IsManualMode { get; set; }
    [ObservableProperty] public partial bool IsFavoritesMode { get; set; }
    [ObservableProperty] public partial string ListText { get; set; }
    [ObservableProperty] public partial string DetailName { get; set; }
    [ObservableProperty] public partial string DetailMeta { get; set; }
    [ObservableProperty] public partial string DetailDatum { get; set; }
    [ObservableProperty] public partial string StandardAxisLabel { get; set; }
    [ObservableProperty] public partial bool AxisOrderEnabled { get; set; }
    [ObservableProperty] public partial int SelectedAxisOrderIndex { get; set; }
    [ObservableProperty] public partial string ManualWkt { get; set; }
    [ObservableProperty] public partial bool UnknownVertical { get; set; }
    [ObservableProperty] public partial bool CompoundVerticalEnabled { get; set; }
    [ObservableProperty] public partial CrsSelection? SelectedFavorite { get; set; }
    [ObservableProperty] public partial string WktPreview { get; set; }
    [ObservableProperty] public partial bool CanSelect { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    // Text shown in the compound search boxes; bound TwoWay so restoring a selection fills them in.
    [ObservableProperty] public partial string HorizontalText { get; set; }
    [ObservableProperty] public partial string VerticalText { get; set; }

    // ---- category switching --------------------------------------------------

    partial void OnSelectedCategoryIndexChanged(int value) => ApplyCategory(value);

    private void ApplyCategory(int index)
    {
        if (index < 0 || index >= CategoryDefs.Length) return;
        var cat = CategoryDefs[index];
        _mode = cat.Mode;
        _kind = cat.Kind;

        IsListMode = cat.Mode == "list";
        IsCompoundMode = cat.Mode == "compound";
        IsManualMode = cat.Mode == "manual";
        IsFavoritesMode = cat.Mode == "favorites";

        ClearCandidate();
        ShowNameOnly(string.Empty);
        ResetStandardAxisLabel();

        switch (cat.Mode)
        {
            case "list": PrepareList(); break;
            case "compound": PrepareCompound(); break;
            case "manual": AxisOrderEnabled = false; break;
            case "favorites": AxisOrderEnabled = false; LoadFavorites(); break;
        }
    }

    // ---- list mode -----------------------------------------------------------

    private IReadOnlyList<CrsRow> GetRows(string kind)
    {
        IsBusy = true;
        try { return _crs.Enumerate(kind); }
        finally { IsBusy = false; }
    }

    // Collapse to lowercase alphanumerics so "WGS84" and "WGS 84" compare equal
    // and punctuation ("/", "-", "·") is ignored.
    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    // Token search: every whitespace-separated word must appear (in any order) in the
    // normalized name or code, so "WGS84 30N" finds "WGS 84 / UTM zone 30N".
    private static IEnumerable<CrsRow> Filter(IEnumerable<CrsRow> rows, string text)
    {
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                         .Select(Normalize)
                         .Where(t => t.Length > 0)
                         .ToArray();
        if (tokens.Length == 0) return rows;
        return rows.Where(r =>
        {
            var name = Normalize(r.Name);
            var code = r.Code.ToString();
            return tokens.All(t => name.Contains(t) || code.Contains(t));
        });
    }

    // Load the current list kind's rows and reset the box (mirrors PrepareCompound).
    private void PrepareList()
    {
        AxisOrderEnabled = true;
        _selectedListRow = null;
        ListText = string.Empty;
        ListSuggestions.Clear();
        _listRows = _kind is null ? null : GetRows(_kind).ToList();
    }

    public void UpdateListSuggestions(string text)
    {
        ListSuggestions.Clear();
        if (_listRows is null) return;
        foreach (var r in Filter(_listRows, text).Take(50)) ListSuggestions.Add(r);
    }

    public void ChooseListItem(object? item)
    {
        _selectedListRow = item as CrsRow;
        if (_selectedListRow is null) { ResetStandardAxisLabel(); ClearCandidate(); return; }
        var isHorizontal = _selectedListRow.Kind.Contains("geographic") || _selectedListRow.Kind.Contains("projected");
        AxisOrderEnabled = isHorizontal;
        UpdateStandardAxisLabel(isHorizontal ? _selectedListRow.Code : null);
        LoadListCandidate(_selectedListRow.Code);
    }

    partial void OnSelectedAxisOrderIndexChanged(int value)
    {
        if (_mode == "list" && _selectedListRow is not null) LoadListCandidate(_selectedListRow.Code);
    }

    // Show the catalogue's standard orientation in parentheses on the "Estándar" option,
    // so the user knows what that choice resolves to for the selected CRS.
    private void ResetStandardAxisLabel() => StandardAxisLabel = _localizer.Get("AxisStandard");

    private void UpdateStandardAxisLabel(int? code)
    {
        if (code is null) { ResetStandardAxisLabel(); return; }
        try
        {
            var label = _crs.GetStandardAxisLabel(code.Value);
            StandardAxisLabel = string.IsNullOrEmpty(label)
                ? _localizer.Get("AxisStandard")
                : $"{_localizer.Get("AxisStandard")} ({label})";
        }
        catch { ResetStandardAxisLabel(); }
    }

    private void LoadListCandidate(int code)
    {
        try
        {
            var d = _crs.GetDetails(code);
            ShowDetails(d);
            var wkt = _crs.GetWktWithAxisOrder(code, CurrentAxisOrder());
            SetCandidate(d.Name, wkt);
        }
        catch (Exception ex) { ShowError(ex.Message); ClearCandidate(); }
    }

    private CrsAxisOrder CurrentAxisOrder()
    {
        if (!AxisOrderEnabled) return CrsAxisOrder.Default;
        return SelectedAxisOrderIndex switch
        {
            1 => CrsAxisOrder.EastNorth,
            2 => CrsAxisOrder.NorthEast,
            _ => CrsAxisOrder.Default,
        };
    }

    // ---- compound mode -------------------------------------------------------

    private void PrepareCompound()
    {
        AxisOrderEnabled = false;
        _compoundH = null;
        _compoundV = null;
        UnknownVertical = false;
        CompoundVerticalEnabled = true;
        HorizontalSuggestions.Clear();
        VerticalSuggestions.Clear();
    }

    private List<CrsRow> MergedHorizontal() =>
        _mergedHorizontal ??= GetRows("projected")
            .Concat(GetRows("geographic 2D"))
            .Concat(GetRows("geographic 3D"))
            .OrderBy(r => r.Code).ToList();

    public void UpdateHorizontalSuggestions(string text)
    {
        HorizontalSuggestions.Clear();
        foreach (var r in Filter(MergedHorizontal(), text).Take(50)) HorizontalSuggestions.Add(r);
    }

    public void ChooseHorizontal(object? item)
    {
        _compoundH = item as CrsRow;
        RecomputeCompound();
    }

    public void UpdateVerticalSuggestions(string text)
    {
        VerticalSuggestions.Clear();
        foreach (var r in Filter(GetRows("vertical"), text).Take(50)) VerticalSuggestions.Add(r);
    }

    public void ChooseVertical(object? item)
    {
        _compoundV = item as CrsRow;
        RecomputeCompound();
    }

    partial void OnUnknownVerticalChanged(bool value)
    {
        CompoundVerticalEnabled = !value;
        RecomputeCompound();
    }

    private void RecomputeCompound()
    {
        if (_compoundH is null) { ClearCandidate(); ShowNameOnly(string.Empty); return; }
        try
        {
            try { ShowDetails(_crs.GetDetails(_compoundH.Code)); } catch { /* keep going */ }

            if (UnknownVertical)
            {
                var wkt = _crs.GetWktWithAxisOrder(_compoundH.Code, CrsAxisOrder.Default);
                SetCandidate(_compoundH.Name, wkt);
            }
            else if (_compoundV is not null)
            {
                var wkt = _crs.GetCompoundWkt(_compoundH.Code, _compoundV.Code);
                SetCandidate($"{_compoundH.Name} + {_compoundV.Name}", wkt);
            }
            else
            {
                ClearCandidate();
            }
        }
        catch (Exception ex) { ShowError(ex.Message); ClearCandidate(); }
    }

    // ---- manual mode ---------------------------------------------------------

    [RelayCommand]
    private void ValidateManual()
    {
        var wkt = (ManualWkt ?? string.Empty).Trim();
        if (wkt.Length == 0) { ClearCandidate(); ShowNameOnly(string.Empty); return; }
        try
        {
            var name = _crs.GetNameOfWkt(wkt);
            ShowNameOnly(name);
            SetCandidate(name, wkt);
        }
        catch (Exception ex) { ShowError(ex.Message); ClearCandidate(); }
    }

    [RelayCommand]
    private async Task ImportPrjAsync()
    {
        var wkt = await _files.ImportPrjAsync();
        if (wkt is null) return;
        ManualWkt = wkt;
        ValidateManual();
    }

    // ---- favorites mode ------------------------------------------------------

    private void LoadFavorites()
    {
        Favorites.Clear();
        foreach (var f in _favorites.Load()) Favorites.Add(f);
    }

    partial void OnSelectedFavoriteChanged(CrsSelection? value)
    {
        if (value is null) return;
        ShowNameOnly(value.DisplayName);
        SetCandidate(value.DisplayName, value.Wkt);
    }

    [RelayCommand]
    private void RemoveFavorite()
    {
        if (SelectedFavorite is null) return;
        _favorites.Remove(SelectedFavorite.DisplayName);
        LoadFavorites();
        ClearCandidate();
        ShowNameOnly(string.Empty);
    }

    // ---- action bar ----------------------------------------------------------

    [RelayCommand]
    private void CopyWkt()
    {
        if (_candidateWkt.Length == 0) return;
        _clipboard.SetText(_candidateWkt);
    }

    [RelayCommand]
    private async Task ExportPrjAsync()
    {
        if (_candidateWkt.Length == 0) return;
        var safe = new string((_candidateName.Length == 0 ? "crs" : _candidateName)
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        await _files.ExportPrjAsync(_candidateWkt, safe);
    }

    [RelayCommand]
    private void Memorize()
    {
        if (_candidateWkt.Length == 0) return;
        _favorites.Add(new CrsSelection(_candidateName, _candidateWkt));
    }

    // ---- confirmation / shared state ----------------------------------------

    // Called when the primary button is clicked; returns false to keep the dialog open.
    public bool Confirm()
    {
        if (_candidateWkt.Length == 0) { Result = null; return false; }

        // Record how this selection was made so the picker can be reopened on the same option.
        var pick = _mode switch
        {
            "list" => new CrsPickState(SelectedCategoryIndex, Code: _selectedListRow?.Code, AxisOrder: SelectedAxisOrderIndex),
            "compound" => new CrsPickState(SelectedCategoryIndex,
                              HorizontalCode: _compoundH?.Code,
                              VerticalCode: UnknownVertical ? null : _compoundV?.Code,
                              UnknownVertical: UnknownVertical),
            "manual" => new CrsPickState(SelectedCategoryIndex, ManualWkt: _candidateWkt),
            _ => SelectedFavorite?.Pick,   // favorites: keep the favorite's own recipe
        };

        Result = new CrsSelection(_candidateName, _candidateWkt, pick);
        return true;
    }

    // Reopen the picker on a previous selection's category with its choices filled in.
    public void Restore(CrsSelection? current)
    {
        if (current?.Pick is not { } pick) return;
        if (pick.Category < 0 || pick.Category >= CategoryDefs.Length) return;

        SelectedCategoryIndex = pick.Category;   // switches mode (loads the list / prepares compound)

        switch (CategoryDefs[pick.Category].Mode)
        {
            case "list":
                if (pick.Code is int code)
                {
                    var row = _listRows?.FirstOrDefault(r => r.Code == code);
                    if (row is not null)
                    {
                        ListText = row.Display;                   // fill the search box
                        ChooseListItem(row);                      // loads its candidate + details
                        SelectedAxisOrderIndex = pick.AxisOrder;  // reapplies the chosen axis order
                    }
                }
                break;

            case "compound":
                UnknownVertical = pick.UnknownVertical;
                _compoundH = pick.HorizontalCode is int h
                    ? MergedHorizontal().FirstOrDefault(r => r.Code == h) : null;
                HorizontalText = _compoundH?.Display ?? string.Empty;
                if (!pick.UnknownVertical && pick.VerticalCode is int v)
                {
                    _compoundV = GetRows("vertical").FirstOrDefault(r => r.Code == v);
                    VerticalText = _compoundV?.Display ?? string.Empty;
                }
                RecomputeCompound();
                break;

            case "manual":
                ManualWkt = pick.ManualWkt;
                ValidateManual();
                break;
        }
    }

    private void SetCandidate(string name, string wkt)
    {
        _candidateName = name ?? string.Empty;
        _candidateWkt = wkt ?? string.Empty;
        WktPreview = _candidateWkt;
        CanSelect = _candidateWkt.Length > 0;
    }

    private void ClearCandidate()
    {
        _candidateName = string.Empty;
        _candidateWkt = string.Empty;
        WktPreview = string.Empty;
        CanSelect = false;
    }

    private void ShowDetails(CrsDetail d)
    {
        DetailName = string.IsNullOrEmpty(d.Name) ? _localizer.Get("DetailNoName") : d.Name;

        var meta = new List<string>();
        if (d.Code != 0) meta.Add($"EPSG:{d.Code}");
        if (!string.IsNullOrEmpty(d.Kind)) meta.Add(d.Kind);
        if (!string.IsNullOrEmpty(d.AreaOfUse)) meta.Add(d.AreaOfUse);
        DetailMeta = string.Join("  ·  ", meta);

        var datum = new[] { d.DatumName, d.PrimeMeridian, d.Ellipsoid }
            .Where(s => !string.IsNullOrEmpty(s));
        DetailDatum = string.Join("  ·  ", datum);
    }

    private void ShowNameOnly(string name)
    {
        DetailName = name.Length > 0 ? name : _localizer.Get("DetailNoSelection");
        DetailMeta = string.Empty;
        DetailDatum = string.Empty;
    }

    private void ShowError(string message)
    {
        DetailName = _localizer.Get("DetailError");
        DetailMeta = message;
        DetailDatum = string.Empty;
    }
}
