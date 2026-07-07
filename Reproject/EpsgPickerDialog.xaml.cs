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

    // AutoSuggestBox raises UI-specific args (change reason / chosen item) that don't map
    // to a plain binding; forward them to the view model. Everything else is bound.
    private void OnHorizontalTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) VM.UpdateHorizontalSuggestions(sender.Text);
    }

    private void OnHorizontalChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) =>
        VM.ChooseHorizontal(args.SelectedItem);

    private void OnVerticalTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) VM.UpdateVerticalSuggestions(sender.Text);
    }

    private void OnVerticalChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) =>
        VM.ChooseVertical(args.SelectedItem);

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
