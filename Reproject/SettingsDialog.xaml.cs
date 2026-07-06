using Microsoft.UI.Xaml.Controls;

namespace Reproject;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsViewModel VM { get; }

    public SettingsDialog(SettingsViewModel vm)
    {
        VM = vm;
        InitializeComponent();
    }

    private void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args) => VM.Save();
}
