using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.TemplateEditor.ViewModels;

namespace OldenEra.TemplateEditor.Views;

public partial class ZoneContentPanel : UserControl
{
    public ZoneContentPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Apply StringComparer.Ordinal to the grouped-presets view so
        // category/name ordering matches the Web optgroup order exactly,
        // regardless of the user's culture. Default SortDescriptions use
        // the current culture's collator.
        if (Resources["GroupedPresets"] is CollectionViewSource cvs &&
            cvs.View is ListCollectionView view)
        {
            view.CustomSort = new PresetOrdinalComparer();
        }
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

    /// <summary>
    /// Removes the row's item-VM from its owning scope. The Button's
    /// DataContext is the <see cref="ZoneContentItemViewModel"/>; the
    /// enclosing <see cref="ItemsControl"/>'s DataContext is the
    /// owning <see cref="ZoneContentScopeViewModel"/>.
    /// </summary>
    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not ZoneContentItemViewModel item) return;
        var owner = FindAncestor<ItemsControl>(btn);
        if (owner?.DataContext is not ZoneContentScopeViewModel scope) return;
        scope.Items.Remove(item);
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var d = VisualTreeHelper.GetParent(start); d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is T t) return t;
        return null;
    }

    private sealed class PresetOrdinalComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not ZoneContentPreset a || y is not ZoneContentPreset b) return 0;
            var c = StringComparer.Ordinal.Compare(a.Category, b.Category);
            return c != 0 ? c : StringComparer.Ordinal.Compare(a.Name, b.Name);
        }
    }
}
