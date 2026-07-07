using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Reproject;

public sealed partial class EpsgPickerDialog : ContentDialog
{
    public EpsgPickerViewModel VM { get; }

    public EpsgPickerDialog(EpsgPickerViewModel vm)
    {
        VM = vm;
        InitializeComponent();
    }

    private void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!VM.Confirm()) args.Cancel = true;
    }

    // Double-clicking a row in a list of systems confirms it and closes the dialog,
    // as a shortcut for selecting it and pressing "Seleccionar".
    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (VM.Confirm()) Hide();
    }
}
