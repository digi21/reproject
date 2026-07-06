using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Reproject;

public sealed partial class OperationPickerDialog : ContentDialog
{
    public OperationPickerViewModel VM { get; }

    public OperationPickerDialog(OperationPickerViewModel vm)
    {
        VM = vm;
        InitializeComponent();
    }

    private void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!VM.Confirm()) args.Cancel = true;
    }

    // Double-clicking an operation confirms it and closes the dialog, as a shortcut
    // for selecting it and pressing the primary button.
    private void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (VM.Confirm()) Hide();
    }
}
