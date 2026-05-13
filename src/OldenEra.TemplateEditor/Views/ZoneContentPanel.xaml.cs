using System.Windows.Controls;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.TemplateEditor.ViewModels;

namespace OldenEra.TemplateEditor.Views;

public partial class ZoneContentPanel : UserControl
{
    public ZoneContentPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles selection in the per-tab "Add from preset" ComboBox.
    /// The ComboBox's <c>Tag</c> carries the target scope-VM (set in XAML
    /// via <c>Tag="{Binding}"</c> while DataContext is the scope-VM).
    /// Resets selection to null so the user can pick the same preset twice.
    /// Warnings refresh on the next CommitToSettings (wired in A10).
    /// </summary>
    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.SelectedItem is not ZoneContentPreset preset) return;
        if (cb.Tag is not ZoneContentScopeViewModel scope) return;
        scope.AddPreset(preset);
        cb.SelectedItem = null;
    }
}
