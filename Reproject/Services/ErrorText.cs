using System;
using System.ComponentModel;

namespace Reproject;

public static class ErrorText
{
    // A WinRT error reaches .NET with the system text of its HRESULT ("Unspecified error" for E_FAIL) before the text
    // CrsKitInterop gave it. Returns that text alone, or the whole message if it has no such prefix.
    public static string Of(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        var system = new Win32Exception(ex.HResult).Message;
        if (system.Length > 0 && message.StartsWith(system, StringComparison.Ordinal))
        {
            var rest = message[system.Length..].TrimStart('\r', '\n', ' ');
            if (rest.Length > 0) return rest;
        }
        return message;
    }
}
