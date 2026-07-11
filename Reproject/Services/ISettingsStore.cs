using System.Collections.Generic;

namespace Reproject;

// Remembers the last-used source/target CRS across runs, the EPSG operation the user picked to go
// between them (when the pair is ambiguous), the recently-used systems (for the main-page dropdowns),
// plus the optional user-chosen directory that holds the geoid/grid models.
public interface ISettingsStore
{
    (CrsSelection? Source, CrsSelection? Target, IReadOnlyList<CrsSelection> Recent, int? OperationCode) Load();
    void Save(CrsSelection? source, CrsSelection? target, IReadOnlyList<CrsSelection> recent, int? operationCode);

    // User-chosen folder holding the geoid/grid model files; null when unset (use the default
    // next to the EPSG database).
    string? LoadGeoidModelsDirectory();
    void SaveGeoidModelsDirectory(string? directory);
}
