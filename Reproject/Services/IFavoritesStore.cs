using System.Collections.Generic;

namespace Reproject;

// Persistent "memorized" CRS list (name -> WKT), the equivalent of nativo's registry favorites.
public interface IFavoritesStore
{
    IReadOnlyList<CrsSelection> Load();
    void Add(CrsSelection selection);
    void Remove(string displayName);
}
