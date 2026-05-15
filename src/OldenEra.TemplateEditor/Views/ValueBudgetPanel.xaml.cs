using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

/// <summary>
/// T-701 — Zone value budget summary, WPF parity for
/// <c>OldenEra.Web.Components.ValueBudgetPanel</c>. Reads only what the
/// generator already emitted; no recomputation. Hidden via
/// <see cref="UIElement.Visibility"/> until <see cref="Update"/> is called
/// with a non-null template that has at least one zone.
/// </summary>
public partial class ValueBudgetPanel : UserControl
{
    public ValueBudgetPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Re-render the panel from a freshly generated template. Call from the
    /// host immediately after each generation. Pass <c>null</c> to hide.
    /// </summary>
    public void Update(RmgTemplate? template)
    {
        var report = TemplateAnalysis.ComputeValueBudget(template);
        if (!report.HasData)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        TxtTotal.Text = "total " + report.Totals.Combined.ToString("N0", CultureInfo.InvariantCulture);

        // Rebuild grid rows. Cheaper to clear + re-add than to diff — this
        // panel updates at most once per Generate click.
        GridRows.Children.Clear();
        GridRows.RowDefinitions.Clear();

        AddHeaderRow();
        foreach (var z in report.Zones)
        {
            AddDataRow(
                z.ZoneName,
                FormatPair(z.ResourcesValue, z.ResourcesValuePerArea),
                FormatPair(z.GuardedContentValue, z.GuardedContentValuePerArea),
                FormatPair(z.UnguardedContentValue, z.UnguardedContentValuePerArea),
                isTotals: false);
        }

        AddDataRow(
            "Totals",
            report.Totals.ResourcesValue.ToString("N0", CultureInfo.InvariantCulture),
            report.Totals.GuardedContentValue.ToString("N0", CultureInfo.InvariantCulture),
            report.Totals.UnguardedContentValue.ToString("N0", CultureInfo.InvariantCulture),
            isTotals: true);
    }

    private void AddHeaderRow()
    {
        GridRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = GridRows.RowDefinitions.Count - 1;

        AddCell("Zone", row, 0, headerStyle: true);
        AddCell("Resources", row, 1, headerStyle: true);
        AddCell("Guarded", row, 2, headerStyle: true);
        AddCell("Unguarded", row, 3, headerStyle: true);
    }

    private void AddDataRow(string name, string resources, string guarded, string unguarded, bool isTotals)
    {
        GridRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = GridRows.RowDefinitions.Count - 1;

        string styleKey = isTotals ? "VbCellTotal" : "VbCell";
        AddCell(name, row, 0, headerStyle: false, styleKey: styleKey);
        AddCell(resources, row, 1, headerStyle: false, styleKey: styleKey);
        AddCell(guarded, row, 2, headerStyle: false, styleKey: styleKey);
        AddCell(unguarded, row, 3, headerStyle: false, styleKey: styleKey);
    }

    private void AddCell(string text, int row, int col, bool headerStyle, string? styleKey = null)
    {
        var tb = new TextBlock { Text = text };
        var key = headerStyle ? "VbHeader" : (styleKey ?? "VbCell");
        if (Resources[key] is Style s)
            tb.Style = s;
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        GridRows.Children.Add(tb);
    }

    private static string FormatPair(int? scalar, int? perArea)
    {
        string s = scalar?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";
        if (perArea is null) return s;
        return $"{s} ({perArea.Value.ToString("N0", CultureInfo.InvariantCulture)}/area)";
    }
}
