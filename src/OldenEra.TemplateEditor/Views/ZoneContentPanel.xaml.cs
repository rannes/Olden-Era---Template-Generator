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
        // the current culture's collator. Loaded fires every time we re-enter
        // the visual tree (e.g. tab switches), so guard against re-applying.
        if (Resources["GroupedPresets"] is CollectionViewSource cvs &&
            cvs.View is ListCollectionView view &&
            view.CustomSort is null)
        {
            view.CustomSort = new PresetOrdinalComparer();
        }

        // Keep SID-catalog groups in ZoneContentSidCatalog.OrderedCategories order
        // (matches Web datalist iteration). Within a category, preserve seed
        // order by SID-string ordinal — the seed is hand-authored in source order
        // already, but ListCollectionView grouping would otherwise alphabetise.
        if (Resources["GroupedSidCatalog"] is CollectionViewSource sidCvs &&
            sidCvs.View is ListCollectionView sidView &&
            sidView.CustomSort is null)
        {
            sidView.CustomSort = new SidCatalogOrdinalComparer();
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
    /// enclosing rows-<see cref="ItemsControl"/> binds its DataContext to
    /// the owning <see cref="ZoneContentScopeViewModel"/>. The row template
    /// also contains an inner ItemsControl for the warnings tooltip — walk
    /// up by DataContext type rather than the first ItemsControl we hit so
    /// that template change can't silently break this lookup.
    /// </summary>
    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not ZoneContentItemViewModel item) return;
        var scope = FindAncestorWithDataContext<ZoneContentScopeViewModel>(btn);
        if (scope is null) return;
        scope.Items.Remove(item);
    }

    private static T? FindAncestorWithDataContext<T>(DependencyObject start) where T : class
    {
        for (var d = VisualTreeHelper.GetParent(start); d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is FrameworkElement fe && fe.DataContext is T t) return t;
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

    /// <summary>
    /// Orders SID-catalog entries first by their category's index in
    /// <see cref="ZoneContentSidCatalog.OrderedCategories"/> (so picker groups
    /// appear Mandatory → Mines → ... → Misc rather than alphabetically), then
    /// by their position in <see cref="ZoneContentSidCatalog.All"/> so the
    /// hand-authored seed order is preserved within a category — matching the
    /// Web side, which iterates <c>Grouped()</c> directly.
    /// </summary>
    private sealed class SidCatalogOrdinalComparer : IComparer
    {
        private static readonly System.Collections.Generic.Dictionary<string, int> CategoryIndex =
            BuildCategoryIndex();
        private static readonly System.Collections.Generic.Dictionary<string, int> SeedIndex =
            BuildSeedIndex();

        private static System.Collections.Generic.Dictionary<string, int> BuildCategoryIndex()
        {
            var dict = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            int i = 0;
            foreach (var c in ZoneContentSidCatalog.OrderedCategories) dict[c] = i++;
            return dict;
        }

        private static System.Collections.Generic.Dictionary<string, int> BuildSeedIndex()
        {
            var dict = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            int i = 0;
            foreach (var entry in ZoneContentSidCatalog.All()) dict[entry.Sid] = i++;
            return dict;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ZoneContentSidEntry a || y is not ZoneContentSidEntry b) return 0;
            int ai = CategoryIndex.TryGetValue(a.Category, out var avi) ? avi : int.MaxValue;
            int bi = CategoryIndex.TryGetValue(b.Category, out var bvi) ? bvi : int.MaxValue;
            int c = ai.CompareTo(bi);
            if (c != 0) return c;
            int asi = SeedIndex.TryGetValue(a.Sid, out var asv) ? asv : int.MaxValue;
            int bsi = SeedIndex.TryGetValue(b.Sid, out var bsv) ? bsv : int.MaxValue;
            return asi.CompareTo(bsi);
        }
    }
}
