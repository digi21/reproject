using System.Collections.Generic;

namespace Reproject;

// The application's gateway to the CRS engine. Wraps the static CrsKitInterop.CrsEngine
// behind an interface so the view models can be constructed (and tested) without the
// process-wide native singleton.
public interface ICrsService
{
    // Locate the EPSG database and initialize the engine if needed. Returns a
    // human-readable status message (never throws).
    string EnsureInitialized();

    // EPSG Geodetic Parameter Dataset version of the active database (e.g. "12.057"); "" if unknown.
    string GetEpsgVersion();

    IReadOnlyList<CrsRow> Enumerate(string kind);
    CrsDetail GetDetails(int code);
    string GetWktWithAxisOrder(int code, CrsAxisOrder order);
    string GetCompoundWkt(int horizontalCode, int verticalCode);
    string GetNameOfWkt(string wkt);
    string DescribeAxes(string wkt);
    string GetStandardAxisLabel(int code);
    ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt);

    // The candidate EPSG operations when MORE THAN ONE exists between the two systems
    // (empty when zero or exactly one — no choice to make). For the operation-selection dialog.
    IReadOnlyList<TransformationOption> GetCandidateOperations(string sourceWkt, string targetWkt);
    // Build the transformation using a specific EPSG operation code chosen by the user.
    ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt, int operationCode);

    // Effective directory (no trailing separator) where geoid/grid model files are searched.
    string GetGeoidModelsDirectory();
}
