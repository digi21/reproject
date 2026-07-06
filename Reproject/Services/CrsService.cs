using System;
using System.Collections.Generic;
using System.IO;
using CrsKitInterop;

namespace Reproject;

// ICrsService backed by the native CrsKitInterop.CrsEngine. Maps the WinRT projected
// types to app-side models and memoizes the (slow) per-kind enumeration.
public sealed class CrsService(ILocalizer localizer, ISettingsStore settings) : ICrsService
{
    private readonly ILocalizer _localizer = localizer;
    private readonly ISettingsStore _settings = settings;
    private readonly Dictionary<string, IReadOnlyList<CrsRow>> _enumerationCache = new();

    public string EnsureInitialized()
    {
        if (CrsEngine.IsInitialized) return _localizer.Get("EpsgReady");
        try
        {
            var path = ResolveSqlitePath();
            // CrsKit prepends this directory to grid/data file names by plain concatenation, so it
            // must end with a separator (e.g. "...\Grids\") — Path.GetDirectoryName drops it.
            var dataDirectory = ResolveModelsDirectory();
            if (dataDirectory.Length > 0 && !dataDirectory.EndsWith(Path.DirectorySeparatorChar))
                dataDirectory += Path.DirectorySeparatorChar;
            CrsEngine.Initialize(path, dataDirectory);
            return _localizer.Format("EpsgPath", path);
        }
        catch (Exception ex)
        {
            return _localizer.Format("EpsgError", ex.Message);
        }
    }

    // Effective geoid/grid models directory (no trailing separator): the user's configured folder
    // if set, otherwise a writable per-user folder. Shown as the default in Settings.
    public string GetGeoidModelsDirectory() => ResolveModelsDirectory();

    public string GetEpsgVersion() => CrsEngine.GetEpsgVersion();

    private string ResolveModelsDirectory()
    {
        var configured = _settings.LoadGeoidModelsDirectory();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        // Downloaded geoid grids must live in a writable per-user folder: when the app is packaged
        // (MSIX) the install directory beside the EPSG database is read-only, so defaulting to the
        // database folder would make grid downloads fail. Use %LOCALAPPDATA%\Reproject\Grids and
        // ensure it exists (CrsKit locates grids by concatenating this directory with the file name).
        var grids = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Reproject", "Grids");
        try { Directory.CreateDirectory(grids); } catch { /* best effort; download will surface any failure */ }
        return grids;
    }

    public IReadOnlyList<CrsRow> Enumerate(string kind)
    {
        if (_enumerationCache.TryGetValue(kind, out var cached)) return cached;

        var list = new List<CrsRow>();
        try
        {
            foreach (var c in CrsEngine.Enumerate(kind))
                list.Add(new CrsRow(c.Code, c.Name, c.Kind));
        }
        catch { /* a kind may be absent in a given dataset */ }

        list.Sort((a, b) => a.Code.CompareTo(b.Code));
        _enumerationCache[kind] = list;
        return list;
    }

    public CrsDetail GetDetails(int code)
    {
        var d = CrsEngine.GetDetails(code);
        return new CrsDetail(d.Code, d.Name, d.Kind, d.AxisCount, d.AreaOfUse,
            d.DatumName, d.DatumOrigin, d.PrimeMeridian, d.Ellipsoid);
    }

    public string GetWktWithAxisOrder(int code, CrsAxisOrder order) =>
        CrsEngine.GetWktWithAxisOrder(code, Map(order));

    public string GetCompoundWkt(int horizontalCode, int verticalCode) =>
        CrsEngine.GetCompoundWkt(horizontalCode, verticalCode);

    public string GetNameOfWkt(string wkt) => CrsEngine.GetNameOfWkt(wkt);

    public string DescribeAxes(string wkt) => CrsEngine.DescribeAxes(wkt);

    public string GetStandardAxisLabel(int code) => CrsEngine.GetStandardAxisLabel(code);

    public ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt) =>
        new TransformationAdapter(CrsEngine.CreateTransformationFromWkt(sourceWkt, targetWkt));

    public IReadOnlyList<TransformationOption> GetCandidateOperations(string sourceWkt, string targetWkt)
    {
        var list = new List<TransformationOption>();
        foreach (var op in CrsEngine.GetCandidateOperations(sourceWkt, targetWkt))
            list.Add(new TransformationOption(
                op.Code, op.Name, op.Type, op.Accuracy, op.Scope, op.Remarks, op.MethodCode,
                op.AreaOfUse, op.GridFiles));
        return list;
    }

    public ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt, int operationCode) =>
        new TransformationAdapter(
            CrsEngine.CreateTransformationFromWktWithOperation(sourceWkt, targetWkt, operationCode));

    // The EPSG SQLite path comes from DIGI21_EPSG_SQLITE if set (and existing),
    // otherwise the conventional Digi3D.NET location under ProgramData.
    private static string ResolveSqlitePath()
    {
        // 1. Explicit override (development / custom datasets).
        var fromEnv = Environment.GetEnvironmentVariable("DIGI21_EPSG_SQLITE");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            return fromEnv;

        // 2. Data shipped with the app (EPSG DB + Spain geoid grids live in the same folder,
        //    so the grid search directory resolves to this Data folder too).
        var bundled = Path.Combine(AppContext.BaseDirectory, "Data", "epsg-fiel.sqlite");
        if (File.Exists(bundled))
            return bundled;

        // 3. Conventional Digi3D.NET location.
        return @"C:\ProgramData\Digi3D.NET\OpenGis\epsg-fiel.sqlite";
    }

    private static AxisOrder Map(CrsAxisOrder order) => order switch
    {
        CrsAxisOrder.EastNorth => AxisOrder.EastNorth,
        CrsAxisOrder.NorthEast => AxisOrder.NorthEast,
        _ => AxisOrder.Default,
    };

    private sealed class TransformationAdapter(Transformation inner) : ITransformation
    {
        public bool IsIdentity => inner.IsIdentity;
        public string SourceName => inner.SourceName;
        public string TargetName => inner.TargetName;
        public int SourceDimension => inner.SourceDimension;
        public double[] Transform(double[] point) => inner.Transform(point);
    }
}
