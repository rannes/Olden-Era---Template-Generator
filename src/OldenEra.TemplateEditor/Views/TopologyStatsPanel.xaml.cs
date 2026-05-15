using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

/// <summary>
/// T-705 — Topology graph stats, WPF parity for
/// <c>OldenEra.Web.Components.TopologyPanel</c>. Reads only what the
/// generator already emitted on <see cref="Variant.Connections"/>; no
/// recomputation. Hidden via <see cref="UIElement.Visibility"/> until
/// <see cref="Update"/> is called with a non-null template.
/// </summary>
public partial class TopologyStatsPanel : UserControl
{
    public TopologyStatsPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Re-render the panel from a freshly generated template. Call from the
    /// host immediately after each generation. Pass <c>null</c> to hide.
    /// </summary>
    public void Update(RmgTemplate? template)
    {
        var report = TemplateAnalysis.ComputeTopology(template);
        if (!report.HasData)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        PnlVariants.Children.Clear();

        bool multiVariant = report.Variants.Count > 1;
        foreach (var v in report.Variants)
        {
            if (multiVariant)
            {
                var header = new TextBlock { Text = v.VariantLabel };
                if (Resources["TgVariantHeader"] is Style hs) header.Style = hs;
                PnlVariants.Children.Add(header);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddRow(grid, "Nodes", v.NodeCount.ToString(CultureInfo.InvariantCulture));
            AddRow(grid, "Edges", v.EdgeCount.ToString(CultureInfo.InvariantCulture));
            AddRow(grid, "Avg. degree", v.AverageDegree.ToString("0.00", CultureInfo.InvariantCulture));
            AddRow(grid, "Diameter", v.Diameter?.ToString(CultureInfo.InvariantCulture) ?? "—");
            AddRow(grid, "Components", v.ComponentCount.ToString(CultureInfo.InvariantCulture));
            AddRow(grid, "Chokepoints", v.ArticulationPoints.Count == 0
                ? "—"
                : string.Join(", ", v.ArticulationPoints));

            PnlVariants.Children.Add(grid);
        }
    }

    private void AddRow(Grid grid, string label, string value)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = grid.RowDefinitions.Count - 1;

        var labelTb = new TextBlock { Text = label };
        if (Resources["TgLabel"] is Style ls) labelTb.Style = ls;
        Grid.SetRow(labelTb, row);
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        var valueTb = new TextBlock { Text = value };
        if (Resources["TgValue"] is Style vs) valueTb.Style = vs;
        Grid.SetRow(valueTb, row);
        Grid.SetColumn(valueTb, 1);
        grid.Children.Add(valueTb);
    }
}
