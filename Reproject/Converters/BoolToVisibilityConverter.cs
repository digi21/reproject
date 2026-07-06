using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Reproject;

// true -> Visible, false -> Collapsed. Keeps Visibility out of the view models.
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}
