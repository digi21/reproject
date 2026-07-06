using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Reproject;

// One candidate operation as shown in the picker: the underlying option plus the
// display-ready strings (already localized) the dialog binds to.
public sealed record OperationRow(
    TransformationOption Option, string Name, string MetaText, string Area, string GridFiles,
    string Scope, string Remarks, Uri? DownloadUri, string DownloadLabel)
{
    public bool HasArea => Area.Length > 0;
    public bool HasGridFiles => GridFiles.Length > 0;
    public bool HasScope => Scope.Length > 0;
    public bool HasRemarks => Remarks.Length > 0;
    public bool HasDownload => DownloadUri is not null;
}

// Drives the "which transformation to use" dialog. The candidate operations are listed
// most-precise first (smallest accuracy; unknown accuracy last), and the best one is
// pre-selected so confirming without touching the list picks it.
public sealed partial class OperationPickerViewModel : ObservableObject
{
    public IReadOnlyList<OperationRow> Operations { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    public partial OperationRow? SelectedOperation { get; set; }

    public bool CanSelect => SelectedOperation is not null;

    // Set by Confirm(); stays null on cancel.
    public TransformationOption? Result { get; private set; }

    public OperationPickerViewModel(IReadOnlyList<TransformationOption> candidates, ILocalizer localizer)
    {
        Operations = candidates
            .OrderBy(o => o.Accuracy.HasValue ? 0 : 1)
            .ThenBy(o => o.Accuracy ?? double.MaxValue)
            .Select(o => ToRow(o, localizer))
            .ToList();

        SelectedOperation = Operations.Count > 0 ? Operations[0] : null;
    }

    // True if a row is selected (so the dialog can close); records the pick in Result.
    public bool Confirm()
    {
        if (SelectedOperation is null) return false;
        Result = SelectedOperation.Option;
        return true;
    }

    private static OperationRow ToRow(TransformationOption o, ILocalizer localizer)
    {
        var accuracy = o.Accuracy is double a
            ? localizer.Format("OpAccuracy", a.ToString("0.###", CultureInfo.CurrentCulture))
            : localizer.Get("OpAccuracyUnknown");
        var meta = $"{accuracy}  ·  EPSG:{o.Code}  ·  {localizer.Format("OpMethod", o.MethodCode)}";
        var area = string.IsNullOrEmpty(o.AreaOfUse) ? string.Empty : localizer.Format("OpArea", o.AreaOfUse);
        var gridFiles = string.IsNullOrEmpty(o.GridFiles) ? string.Empty : localizer.Format("OpGridFiles", o.GridFiles);
        var scope = string.IsNullOrEmpty(o.Scope) ? string.Empty : localizer.Format("OpScope", o.Scope);
        var remarks = string.IsNullOrEmpty(o.Remarks) ? string.Empty : localizer.Format("OpRemarks", o.Remarks);

        // Offer a browser link only for sources we cannot fetch automatically (page-only, e.g. EGM2008).
        // Directly downloadable ones (with a fileBase) are installed on selection, so no link here.
        var source = GridDownloadCatalog.Find(o.GridFiles);
        var pageOnly = source is not null && string.IsNullOrEmpty(source.FileBase);
        var downloadUri = pageOnly ? new Uri(source!.Url) : null;
        var downloadLabel = pageOnly ? localizer.Format("OpDownload", source!.Name) : string.Empty;

        return new OperationRow(o, o.Name, meta, area, gridFiles, scope, remarks, downloadUri, downloadLabel);
    }
}
