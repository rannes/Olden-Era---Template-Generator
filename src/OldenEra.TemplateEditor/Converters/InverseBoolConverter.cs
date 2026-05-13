using System;
using System.Globalization;
using System.Windows.Data;

namespace OldenEra.TemplateEditor.Converters;

/// <summary>
/// One-way and two-way <see cref="bool"/> inverter for WPF bindings. Used by
/// the zone-content panel to disable editing controls while
/// <c>ZoneContentPanelViewModel.IsReadOnly</c> is true (defaults-compare mode).
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
