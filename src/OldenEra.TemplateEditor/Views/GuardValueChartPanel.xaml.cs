using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

/// <summary>
/// T-702 — Guard-power vs. zone-value chart, WPF parity for
/// <c>OldenEra.Web.Components.GuardValueChartPanel</c>. Reads only what the
/// generator already emitted onto each <see cref="Zone"/>. Hidden via
/// <see cref="UIElement.Visibility"/> until <see cref="Update"/> is called
/// with a template that has at least one plottable zone.
/// </summary>
public partial class GuardValueChartPanel : UserControl
{
    private const double CanvasW = 360;
    private const double CanvasH = 220;
    private const double PadL = 40;
    private const double PadR = 12;
    private const double PadT = 10;
    private const double PadB = 28;

    public GuardValueChartPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Re-render the chart from a freshly generated template. Pass <c>null</c>
    /// to hide. Cheaper to clear + redraw than to diff — this panel updates at
    /// most once per Generate click.
    /// </summary>
    public void Update(RmgTemplate? template)
    {
        var report = TemplateAnalysis.ComputeGuardChart(template);
        ChartCanvas.Children.Clear();

        if (!report.HasData)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        TxtOutliers.Text = report.OutlierCount == 0
            ? "no outliers"
            : $"{report.OutlierCount} outlier{(report.OutlierCount == 1 ? "" : "s")}";

        // Pad the data range a touch so end points don't sit on the axis.
        double gMin = report.GuardMultiplierMin;
        double gMax = report.GuardMultiplierMax;
        if (gMax - gMin < 1e-6) { gMin -= 0.5; gMax += 0.5; }
        int vMin = report.ResourcesValueMin;
        int vMax = report.ResourcesValueMax;
        if (vMax == vMin) { vMin = System.Math.Max(0, vMin - 1); vMax += 1; }

        double innerW = CanvasW - PadL - PadR;
        double innerH = CanvasH - PadT - PadB;

        double Sx(double g) => PadL + (g - gMin) / (gMax - gMin) * innerW;
        double Sy(double v) => PadT + innerH - (v - vMin) / (double)(vMax - vMin) * innerH;

        var border = TryFindResource("OeBorderBrush") as Brush ?? Brushes.Gray;
        var dim = TryFindResource("OeTextDimBrush") as Brush ?? Brushes.Gray;
        var text = TryFindResource("OeTextBrush") as Brush ?? Brushes.Black;
        var surface = TryFindResource("OeSurface1Brush") as Brush ?? Brushes.White;

        // Plot frame
        var frame = new Rectangle
        {
            Width = innerW,
            Height = innerH,
            Stroke = border,
            StrokeThickness = 1,
            Fill = Brushes.Transparent,
        };
        Canvas.SetLeft(frame, PadL);
        Canvas.SetTop(frame, PadT);
        ChartCanvas.Children.Add(frame);

        // Axis ticks
        AddText(vMax.ToString("N0", CultureInfo.InvariantCulture), 6, PadT - 1, 9, dim);
        AddText(vMin.ToString("N0", CultureInfo.InvariantCulture), 6, PadT + innerH - 5, 9, dim);
        AddText(gMin.ToString("F2", CultureInfo.InvariantCulture), PadL, CanvasH - 18, 9, dim);
        AddText(gMax.ToString("F2", CultureInfo.InvariantCulture), PadL + innerW - 24, CanvasH - 18, 9, dim);
        AddText("guard mult.", PadL, CanvasH - 8, 10, dim);

        // Points: outliers last so they sit on top.
        foreach (var p in report.Points.Where(p => !p.IsOutlier))
            AddPoint(Sx(p.GuardMultiplier), Sy(p.ResourcesValue), 4, text, surface, isOutlier: false, p);

        foreach (var p in report.Points.Where(p => p.IsOutlier))
            AddPoint(Sx(p.GuardMultiplier), Sy(p.ResourcesValue), 5, Brushes.Transparent, surface, isOutlier: true, p);
    }

    private void AddText(string s, double x, double y, double size, Brush fill)
    {
        var tb = new TextBlock
        {
            Text = s,
            FontSize = size,
            Foreground = fill,
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        ChartCanvas.Children.Add(tb);
    }

    private void AddPoint(double cx, double cy, double r, Brush fill, Brush stroke, bool isOutlier, TemplateAnalysis.GuardChartPoint p)
    {
        var dot = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = isOutlier ? new SolidColorBrush(Color.FromRgb(0xD6, 0x27, 0x28)) : fill,
            Stroke = stroke,
            StrokeThickness = isOutlier ? 1.5 : 1,
            Opacity = isOutlier ? 0.9 : 0.7,
            ToolTip = isOutlier
                ? $"OUTLIER — {p.ZoneName}: rich (value {p.ResourcesValue.ToString("N0", CultureInfo.InvariantCulture)}) with weak guards (mult {p.GuardMultiplier.ToString("F2", CultureInfo.InvariantCulture)})"
                : $"{p.ZoneName}: guard {p.GuardMultiplier.ToString("F2", CultureInfo.InvariantCulture)}, value {p.ResourcesValue.ToString("N0", CultureInfo.InvariantCulture)}",
        };
        Canvas.SetLeft(dot, cx - r);
        Canvas.SetTop(dot, cy - r);
        ChartCanvas.Children.Add(dot);
    }
}
