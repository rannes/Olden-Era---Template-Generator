using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Services;

/// <summary>
/// T-803: small helper that pairs a WPF control with a field-help id and sets
/// the resulting <see cref="FrameworkElement.ToolTip"/> from the shared YAML
/// catalog. Mirrors the Web host's <c>title=</c> wiring so both surfaces show
/// the same help text. When the catalog has no entry for the key, the
/// existing tooltip (if any) is left in place — default behavior unchanged.
/// </summary>
public static class FieldHelpAttacher
{
    /// <summary>
    /// Attach a tooltip to <paramref name="control"/> from
    /// <see cref="FieldHelpCatalog.Default"/> using <paramref name="key"/>.
    /// Also attaches the same text to <paramref name="label"/> when supplied
    /// so users hovering the row label see the help too.
    /// </summary>
    public static void Attach(FrameworkElement? control, string key, FrameworkElement? label = null)
    {
        var text = FieldHelpCatalog.Default.For(key);
        if (string.IsNullOrEmpty(text)) return;
        if (control is not null && control.ToolTip is null) control.ToolTip = text;
        if (label is not null && label.ToolTip is null) label.ToolTip = text;
    }
}
