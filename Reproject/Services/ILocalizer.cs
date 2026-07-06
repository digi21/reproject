using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Reproject;

// Localizes code-side strings from Resources.resw (the same catalogue x:Uid uses in XAML).
// Backed by the Windows App SDK ResourceLoader, which works in an unpackaged app and
// follows the OS language.
public interface ILocalizer
{
    string Get(string key);
    string Format(string key, params object[] args);
}

public sealed class ResourceLocalizer : ILocalizer
{
    private readonly ResourceLoader _loader = new();

    public string Get(string key)
    {
        var value = _loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value; // fall back to the key if missing
    }

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
