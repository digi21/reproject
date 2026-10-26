using System.Collections.Generic;

namespace Reproject;

// Wraps the static Digi21.CrsKit.Interop.CrsEngine
// behind an interface so the view models can be constructed (and tested) without the
// process-wide native singleton.
public interface ICrsService
{
    // Locate the EPSG database and initialize the engine if needed. Returns a
    // human-readable status message (never throws).
    string EnsureInitialized();

    // EPSG Geodetic Parameter Dataset version of the active database (e.g. "12.057"); "" if unknown.
    string GetEpsgVersion();

    string DescribeAxes(string wkt);
    ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt);

    // The candidate EPSG operations when MORE THAN ONE exists between the two systems
    // (empty when zero or exactly one — no choice to make). For the operation-selection dialog.
    IReadOnlyList<TransformationOption> GetCandidateOperations(string sourceWkt, string targetWkt);
    // Build the transformation using a specific EPSG operation code chosen by the user.
    ITransformation CreateTransformationFromWkt(string sourceWkt, string targetWkt, int operationCode);

    // Effective directory (no trailing separator) where geoid/grid model files are searched.
    string GetGeoidModelsDirectory();
}
