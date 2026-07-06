using System;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Reproject;

// Purely presentational: static attribution/licensing content. Icons are loaded from
// the deployed Assets folder (file path, robust for an unpackaged app).
public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog(string epsgVersion = "")
    {
        InitializeComponent();
        AppIcon.Source = Load("reproject.png");
        CrsKitIcon.Source = Load("crskit.png");

        if (string.IsNullOrEmpty(epsgVersion))
            EpsgVersionText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        else
            EpsgVersionText.Text = $"EPSG Dataset v{epsgVersion}";
    }

    private static BitmapImage Load(string file) =>
        new(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", file)));
}
