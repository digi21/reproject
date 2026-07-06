using System.Collections.Generic;

namespace Reproject;

// Remembers the last-used source/target CRS across runs, the recently-used systems (for the
// main-page dropdowns), plus the optional user-chosen directory that holds the geoid/grid models.
public interface ISettingsStore
{
    (CrsSelection? Source, CrsSelection? Target, IReadOnlyList<CrsSelection> Recent) Load();
    void Save(CrsSelection? source, CrsSelection? target, IReadOnlyList<CrsSelection> recent);

    // User-chosen folder holding the geoid/grid model files; null when unset (use the default
    // next to the EPSG database).
    string? LoadGeoidModelsDirectory();
    void SaveGeoidModelsDirectory(string? directory);
}
