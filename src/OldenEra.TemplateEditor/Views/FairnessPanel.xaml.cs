using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

/// <summary>
/// T-704 — Per-player fairness audit, WPF parity for
/// <c>OldenEra.Web.Components.FairnessPanel</c>. Reads only what the
/// generator already emitted (Spawn MainObjects, Connections, GameRules
/// bonuses); no recomputation. Hidden via <see cref="UIElement.Visibility"/>
/// until <see cref="Update"/> is called with a non-null template that has
/// at least one player zone.
/// </summary>
public partial class FairnessPanel : UserControl
{
    public FairnessPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Re-render the panel from a freshly generated template. Call from the
    /// host immediately after each generation. Pass <c>null</c> to hide.
    /// </summary>
    public void Update(RmgTemplate? template)
    {
        var report = TemplateAnalysis.ComputeFairness(template);
        if (!report.HasData)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        int outliers = report.Players.Count(p => p.IsOutlier);
        if (outliers == 0)
        {
            TxtStatus.Text = "balanced";
            TxtStatus.SetResourceReference(
                TextBlock.ForegroundProperty, "OeTextDimBrush");
        }
        else
        {
            TxtStatus.Text = outliers == 1 ? "1 outlier" : $"{outliers} outliers";
            TxtStatus.SetResourceReference(
                TextBlock.ForegroundProperty, "OeErrorBrush");
        }

        TxtHint.Text = string.Format(
            CultureInfo.InvariantCulture,
            "Median: neighbours {0:0.#}, castles {1:0.#}, yield {2:N0}. " +
            "Cells flag when they deviate by more than {3:0}% of the median.",
            report.Medians.NeighborCount,
            report.Medians.StartingCastleCount,
            report.Medians.ResourceYield,
            report.DeviationPercent);

        // Cheaper to clear + rebuild than to diff — panel updates at most
        // once per Generate click.
        GridRows.Children.Clear();
        GridRows.RowDefinitions.Clear();

        AddHeaderRow();
        foreach (var p in report.Players)
        {
            AddDataRow(p);
        }
    }

    private void AddHeaderRow()
    {
        GridRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = GridRows.RowDefinitions.Count - 1;

        AddCell("Player", row, 0, headerStyle: true);
        AddCell("Neighbours", row, 1, headerStyle: true);
        AddCell("Castles", row, 2, headerStyle: true);
        AddCell("Yield", row, 3, headerStyle: true);
    }

    private void AddDataRow(TemplateAnalysis.PlayerFairnessRow p)
    {
        GridRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = GridRows.RowDefinitions.Count - 1;

        string label = $"P{p.Slot}  {p.ZoneName}";
        AddCell(
            label,
            row,
            0,
            headerStyle: false,
            styleKey: p.IsOutlier ? "FaCellFlag" : "FaCell");
        AddCell(
            p.NeighborCount.ToString("N0", CultureInfo.InvariantCulture),
            row,
            1,
            headerStyle: false,
            styleKey: p.NeighborOutlier ? "FaCellFlag" : "FaCell");
        AddCell(
            p.StartingCastleCount.ToString("N0", CultureInfo.InvariantCulture),
            row,
            2,
            headerStyle: false,
            styleKey: p.StartingCastleOutlier ? "FaCellFlag" : "FaCell");
        AddCell(
            p.ResourceYield.ToString("N0", CultureInfo.InvariantCulture),
            row,
            3,
            headerStyle: false,
            styleKey: p.ResourceYieldOutlier ? "FaCellFlag" : "FaCell");
    }

    private void AddCell(string text, int row, int col, bool headerStyle, string? styleKey = null)
    {
        var tb = new TextBlock { Text = text };
        var key = headerStyle ? "FaHeader" : (styleKey ?? "FaCell");
        if (Resources[key] is Style s)
            tb.Style = s;
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        GridRows.Children.Add(tb);
    }
}
