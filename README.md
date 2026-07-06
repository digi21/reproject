<!-- Tip: add a screenshot to make this shine, e.g. ![Reproject](docs/screenshot.png) -->
<p align="center">
  <img src="Reproject/Assets/reproject.png" width="96" alt="Reproject icon" />
</p>

<h1 align="center">Reproject</h1>

<p align="center">Convert coordinates between reference systems — a fast, modern Windows app.</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-0078D6">
  <img alt="WinUI 3" src="https://img.shields.io/badge/UI-WinUI%203-5C2D91">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="Built on CrsKit" src="https://img.shields.io/badge/engine-CrsKit-0f9d8f">
</p>

---

**Reproject** transforms geographic and projected coordinates between reference systems (CRS),
backed by the full EPSG catalogue. It is a WinUI 3 desktop app built on the
[**CrsKit**](https://github.com/digi21/crskit) geodesy library.

## Features

- **Rich EPSG picker** — pick source and target systems by searching on EPSG code or name,
  browsing by category (geographic 2D/3D, projected, vertical), building a compound system
  (horizontal + vertical), pasting raw WKT, or reusing favorites. Double-click to select.
- **Live transformation** — results update line by line as you type or paste.
- **Import / export** — coordinate lists (CSV/TXT) and CRS definitions (`.prj` / WKT).
- **Remembers your last-used systems** between runs.
- **Axis orientation** control and a details pane (area of use, datum, ellipsoid, units).
- Follows the OS **light/dark theme** and **language**.

## Languages

Localized into 8 languages, following the OS language (English fallback): English, Spanish,
Galician, Basque, Catalan, Italian, French, German.

## Built on CrsKit

The coordinate engine is [CrsKit](https://github.com/digi21/crskit), a modern C++
geodesy/CRS library, consumed through its **vcpkg** package and wrapped for WinUI via a
C++/WinRT component.

```
Reproject                (C# WinUI 3 app)
  → CrsKitInterop.Projection   (C#/WinRT projection)
     → CrsKitInterop           (C++/WinRT component)
        → vcpkg: crskit (+ sqlite3)
```

## Coordinate data (EPSG)

This product includes the **EPSG Geodetic Parameter Dataset**, owned by IOGP (International
Association of Oil & Gas Producers), used under the *EPSG Dataset Terms of Use*. See the
in-app **About** dialog for the full attribution and disclaimer.

## Tech

WinUI 3 · .NET 10 · MVVM (CommunityToolkit.Mvvm) + dependency injection · C++/WinRT interop ·
vcpkg · SQLite (EPSG).

## Build

1. **Prerequisites:** Visual Studio (WinUI / Windows App SDK workload, toolset v145) and
   **vcpkg** with `VCPKG_ROOT` set. The `crskit` port is an overlay port of the CrsKit repo;
   its path is set in `CrsKitInterop/vcpkg-configuration.json` — adjust it if the library
   repo lives elsewhere.
2. Provide the EPSG database `epsg-fiel.sqlite`. The app looks for it in the
   `DIGI21_EPSG_SQLITE` environment variable, then at
   `C:\ProgramData\Digi3D.NET\OpenGis\epsg-fiel.sqlite`.
3. Open **`Reproject.sln`** (x64), restore packages, and build. Building `CrsKitInterop`
   makes vcpkg install/compile `crskit`.
4. Run **Reproject** (F5).

> The native DLLs (CrsKitInterop, CrsKit and sqlite3) are shipped next to the executable and
> inside the MSIX payload as `Content` items in the app's `.csproj`.

## License

Reproject is licensed under the [Apache License 2.0](LICENSE).

Copyright 2015-2026 Digi21 (José Ángel Martínez Torres).

This product includes the EPSG Geodetic Parameter Dataset, owned by IOGP and used
under the EPSG Dataset Terms of Use. The EPSG data is not covered by the Apache
license above.
