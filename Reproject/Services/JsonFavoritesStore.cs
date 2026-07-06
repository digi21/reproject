using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Reproject;

// IFavoritesStore stored as JSON (name -> WKT) under %LOCALAPPDATA% because an unpackaged
// app has no ApplicationData.Current.
public sealed class JsonFavoritesStore : IFavoritesStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Reproject", "favorites.json");

    public IReadOnlyList<CrsSelection> Load()
    {
        var map = LoadMap();
        return map.Select(kv => new CrsSelection(kv.Key, kv.Value))
                  .OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                  .ToList();
    }

    public void Add(CrsSelection selection)
    {
        var map = LoadMap();
        map[selection.DisplayName] = selection.Wkt; // name is the key, like nativo's "memorized" entries
        Save(map);
    }

    public void Remove(string displayName)
    {
        var map = LoadMap();
        if (map.Remove(displayName)) Save(map);
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

    private static void Save(Dictionary<string, string> map)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
    }
}
