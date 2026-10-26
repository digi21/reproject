using System;
using System.Collections.Generic;
using System.IO;
using Digi21.CrsKit.Interop;

namespace Reproject;

// ICrsService backed by the native Digi21.CrsKit.Interop.CrsEngine. Maps the WinRT projected
// types to app-side models.
public sealed class CrsService(ILocalizer localizer, ISettingsStore settings) : ICrsService
{
    private readonly ILocalizer _localizer = localizer;
    private readonly ISettingsStore _settings = settings;

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
            return _localizer.Format("EpsgError", ErrorText.Of(ex));
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

    public string DescribeAxes(string wkt) => CrsEngine.DescribeAxes(wkt);

    public ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt) =>
        new TransformationAdapter(CrsEngine.CreateTransformationFromWkt(sourceWkt, targetWkt));

    public IReadOnlyList<TransformationOption> GetCandidateOperations(string sourceWkt, string targetWkt)
    {
        var list = new List<TransformationOption>();
        foreach (var op in CrsEngine.GetCandidateOperations(sourceWkt, targetWkt))
            list.Add(new TransformationOption(
                op.Code, op.Name, op.Type, op.AccuracyMetres, op.Scope, op.Remarks, op.MethodCode,
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

    private sealed class TransformationAdapter(Transformation inner) : ITransformation
    {
        public bool IsIdentity => inner.IsIdentity;
        public string SourceName => inner.SourceName;
        public string TargetName => inner.TargetName;
        public int SourceDimension => inner.SourceDimension;
        public double[] Transform(double[] point) => inner.Transform(point);
    }
}
