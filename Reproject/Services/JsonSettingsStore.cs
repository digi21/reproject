using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Reproject;

// ISettingsStore persisted as a flat JSON string map under %LOCALAPPDATA% (an unpackaged
// app has no ApplicationData.Current), mirroring JsonFavoritesStore.
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Reproject", "settings.json");

    public (CrsSelection? Source, CrsSelection? Target, IReadOnlyList<CrsSelection> Recent) Load()
    {
        var map = LoadMap();
        return (Read(map, "SourceName", "SourceWkt", "SourcePick"),
                Read(map, "TargetName", "TargetWkt", "TargetPick"),
                ReadRecent(map));
    }

    public void Save(CrsSelection? source, CrsSelection? target, IReadOnlyList<CrsSelection> recent) => Update(map =>
    {
        Set(map, "SourceName", source?.DisplayName); Set(map, "SourceWkt", source?.Wkt);
        Set(map, "SourcePick", SerializePick(source?.Pick));
        Set(map, "TargetName", target?.DisplayName); Set(map, "TargetWkt", target?.Wkt);
        Set(map, "TargetPick", SerializePick(target?.Pick));
        Set(map, "Recent", recent.Count > 0 ? JsonSerializer.Serialize(recent) : null);
    });

    private static IReadOnlyList<CrsSelection> ReadRecent(Dictionary<string, string> map)
    {
        if (map.TryGetValue("Recent", out var json) && !string.IsNullOrEmpty(json))
        {
            try { return JsonSerializer.Deserialize<List<CrsSelection>>(json) ?? new List<CrsSelection>(); }
            catch { /* stale/invalid list: start empty */ }
        }
        return new List<CrsSelection>();
    }

    public string? LoadGeoidModelsDirectory()
    {
        var map = LoadMap();
        return map.TryGetValue("GeoidModelsDirectory", out var dir) && !string.IsNullOrWhiteSpace(dir) ? dir : null;
    }

    public void SaveGeoidModelsDirectory(string? directory) =>
        Update(map => Set(map, "GeoidModelsDirectory", string.IsNullOrWhiteSpace(directory) ? null : directory));

    // Set or remove a key, then persist. Merges into the existing file so the different settings
    // (last-used CRS, models directory) do not overwrite each other.
    private static void Set(Dictionary<string, string> map, string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) map.Remove(key);
        else map[key] = value;
    }

    private void Update(Action<Dictionary<string, string>> mutate)
    {
        var map = LoadMap();
        mutate(map);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* persisting settings is best-effort */ }
    }

    private static string? SerializePick(CrsPickState? pick) =>
        pick is null ? null : JsonSerializer.Serialize(pick);

    private static CrsSelection? Read(Dictionary<string, string> map, string nameKey, string wktKey, string pickKey)
    {
        if (map.TryGetValue(nameKey, out var name) &&
            map.TryGetValue(wktKey, out var wkt) && !string.IsNullOrEmpty(wkt))
        {
            CrsPickState? pick = null;
            if (map.TryGetValue(pickKey, out var pickJson) && !string.IsNullOrEmpty(pickJson))
            {
                try { pick = JsonSerializer.Deserialize<CrsPickState>(pickJson); }
                catch { /* stale/invalid recipe: fall back to no restore */ }
            }
            return new CrsSelection(name, wkt, pick);
        }
        return null;
    }

    private static Dictionary<string, string> LoadMap()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))
                       ?? new Dictionary<string, string>();
        }
        catch { /* fall through to empty */ }
        return new Dictionary<string, string>();
    }
}
