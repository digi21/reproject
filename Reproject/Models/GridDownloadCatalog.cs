using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace Reproject;

// A known place to obtain a grid file. Match is a case-insensitive substring looked up in a grid
// file name (as reported by EPSG, e.g. "Und_min1x1_egm2008_isw=82_WGS84_TideFree"); Url is the
// official download page the user is sent to. FileBase, when set, is a URL prefix such that
// FileBase + the exact grid file name is a DIRECT download (no licence gate), so the file can be
// fetched straight into the geoid folder instead of opening the page.
public sealed record GridDownloadSource(string Match, string Name, string Url, string FileBase = "");

// Maps grid files an operation needs to an official download page, so the picker can offer a link when
// the required file is one we know where to get. Grid files are agency-licensed and not redistributable,
// so we only ever link to the source's own page — we never host or fetch the file itself.
//
// The list is fetched at runtime from a small JSON in the public crskit repo, so it can be updated (new
// sources, fixed URLs) without rebuilding the app. A bundled default seeds it and is the fallback when
// offline or the fetch fails; the last good fetch is cached locally for the next launch.
public static class GridDownloadCatalog
{
    public const string RemoteUrl = "https://raw.githubusercontent.com/digi21/crskit/main/data/grid-sources.json";
    private const string CacheFileName = "grid-sources.json";

    // Bundled default: works offline / on first run / if the remote fetch fails. Kept in sync with the
    // remote data/grid-sources.json in the crskit repo (the remote copy overrides this at runtime).
    private static readonly GridDownloadSource[] Default =
    {
        new("egm2008", "EGM2008 geoid grid (NGA)",
            "https://earth-info.nga.mil/index.php?dir=wgs84&action=wgs84"),
        new("rednap", "EGM08-REDNAP Spanish geoid (IGN)",
            "https://datos-geodesia.ign.es/geoide/",
            "https://datos-geodesia.ign.es/geoide/ascii/"),
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Swapped as a whole (an atomic reference assignment) when a newer list loads, so Find() always sees
    // a complete, valid array.
    private static volatile GridDownloadSource[] _sources = Default;

    // The first source whose Match appears in the given (comma-separated) grid file name(s); null if
    // none is known.
    public static GridDownloadSource? Find(string gridFiles)
    {
        if (string.IsNullOrEmpty(gridFiles)) return null;
        var haystack = gridFiles.ToLowerInvariant();
        return _sources.FirstOrDefault(s => haystack.Contains(s.Match.ToLowerInvariant()));
    }

    // The direct download URL for one specific grid file (FileBase + the file name), or null when the
    // file's source is unknown or has no direct download (page-only). Used to fetch the file into the
    // geoid folder automatically.
    public static string? DirectUrlFor(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var lower = fileName.ToLowerInvariant();
        var source = _sources.FirstOrDefault(s => lower.Contains(s.Match.ToLowerInvariant()));
        if (source is null || string.IsNullOrEmpty(source.FileBase)) return null;
        return source.FileBase.TrimEnd('/') + "/" + fileName.Trim();
    }

    // Load the cached list (fast, offline) then refresh from the remote in the background. Safe to call
    // once at startup and ignore the returned task; failures leave the bundled/cached list in place.
    public static async Task InitializeAsync()
    {
        TryLoad(await ReadCacheAsync());

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await http.GetStringAsync(RemoteUrl);
            if (TryLoad(json))
                await WriteCacheAsync(json);
        }
        catch
        {
            // Offline or an unusable response: keep whatever we already have (cache or bundled default).
        }
    }

    // Parse + validate a JSON list and, if it yields at least one valid entry, make it the active list.
    private static bool TryLoad(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var parsed = (JsonSerializer.Deserialize<GridDownloadSource[]>(json, JsonOptions) ?? Array.Empty<GridDownloadSource>())
                .Where(IsValid)
                .ToArray();
            if (parsed.Length == 0) return false;
            _sources = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Only trust entries that name a file, a label and an https page; and, if a direct download base is
    // given, it must be https too (we fetch from it or open it in a browser).
    private static bool IsValid(GridDownloadSource s) =>
        !string.IsNullOrWhiteSpace(s.Match) &&
        !string.IsNullOrWhiteSpace(s.Name) &&
        Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (string.IsNullOrEmpty(s.FileBase) ||
         (Uri.TryCreate(s.FileBase, UriKind.Absolute, out var fb) && fb.Scheme == Uri.UriSchemeHttps));

    // The size in bytes of what url would download (its Content-Length), or null if the server does not
    // report it. Used to tell the user how big the download is before starting.
    public static async Task<long?> ProbeSizeAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
        }
        catch
        {
            return null;
        }
    }

    // Download a file (e.g. a grid) to destPath, reporting fractional progress (0..1) when the size is
    // known. Writes to a ".part" temp then moves it into place, so a failed download never leaves a
    // truncated grid. Throws on failure; the caller handles it.
    public static async Task DownloadFileAsync(string url, string destPath, IProgress<double>? progress = null)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = destPath + ".part";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using (var src = await response.Content.ReadAsStreamAsync())
        await using (var dst = File.Create(tmp))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                if (total is long t && t > 0) progress?.Report((double)read / t);
            }
        }

        File.Move(tmp, destPath, overwrite: true);
    }

    private static async Task<string?> ReadCacheAsync()
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, CacheFileName);
            return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteCacheAsync(string json)
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, CacheFileName);
            await File.WriteAllTextAsync(path, json);
        }
        catch
        {
            // A cache write failure is not fatal: the in-memory list is already updated.
        }
    }
}
