using System.ComponentModel;
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
        VM.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => VM.PropertyChanged -= OnViewModelPropertyChanged;
    }

    // A ComboBox does not redraw its closed box when the Content of the selected item changes, so the "Standard" item
    // kept the axes of the previous system. Reselect it once the binding has updated the Content.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EpsgPickerViewModel.StandardAxisLabel)) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (AxisOrderCombo.SelectedIndex != 0) return;
            AxisOrderCombo.SelectedIndex = -1;
            AxisOrderCombo.SelectedIndex = 0;
        });
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
