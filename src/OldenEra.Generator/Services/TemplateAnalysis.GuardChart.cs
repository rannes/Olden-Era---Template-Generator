using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services;

/// <summary>
/// T-702 — Guard-power vs. zone-value analytic.
/// </summary>
/// <remarks>
/// <para>
/// Plots the per-zone effective guard multiplier (<see cref="Zone.GuardMultiplier"/>,
/// already scaled by the global <c>BorderGuardStrengthPercent</c> at emission time)
/// against the per-zone resources budget (<see cref="Zone.ResourcesValue"/>) so a
/// designer can spot the most common balance bug: a high-value zone with a low
/// guard multiplier ("rich zone with weak guards").
/// </para>
/// <para>
/// Outlier rule: a zone is flagged when both
/// <c>ResourcesValue &gt;= P75</c> across plotted zones AND
/// <c>GuardMultiplier &lt;= P25</c> across plotted zones. Percentiles use the
/// type-7 (linear interpolation) convention so a small zone count behaves
/// predictably. The rule is intentionally permissive about cardinality: with
/// fewer than four zones the spread is too small to call anyone an outlier,
/// so no points are flagged.
/// </para>
/// </remarks>
public static partial class TemplateAnalysis
{
    /// <summary>
    /// One plotted point on the guard-vs-value chart. Both numeric axes are
    /// always present — zones missing either field are excluded by
    /// <see cref="ComputeGuardChart"/> and never appear here.
    /// </summary>
    public sealed record GuardChartPoint(
        string ZoneName,
        double GuardMultiplier,
        int ResourcesValue,
        bool IsOutlier);

    /// <summary>
    /// Result of <see cref="ComputeGuardChart"/>. <see cref="Points"/> is empty
    /// when the template has no zones with both <c>guardMultiplier</c> and
    /// <c>resourcesValue</c> set — callers should hide the panel in that case.
    /// </summary>
    /// <remarks>
    /// Axis bounds are exposed so the rendering layer (SVG / WPF Canvas) does
    /// not have to recompute them and so the same numbers drive the gridlines
    /// in both hosts. Bounds default to <c>0..1</c> for an empty result.
    /// </remarks>
    public sealed record GuardChartReport(
        IReadOnlyList<GuardChartPoint> Points,
        double GuardMultiplierMin,
        double GuardMultiplierMax,
        int ResourcesValueMin,
        int ResourcesValueMax)
    {
        public bool HasData => Points.Count > 0;
        public int OutlierCount => Points.Count(p => p.IsOutlier);
    }

    /// <summary>
    /// Compute the guard-power vs. zone-value chart for a generated template.
    /// Reads only the emitted <see cref="Zone.GuardMultiplier"/> and
    /// <see cref="Zone.ResourcesValue"/> — no recomputation, no defaults.
    /// </summary>
    /// <param name="template">A template fresh out of
    /// <see cref="TemplateGenerator.Generate"/>, or <c>null</c>.</param>
    public static GuardChartReport ComputeGuardChart(RmgTemplate? template)
    {
        if (template?.Variants is null || template.Variants.Count == 0)
            return EmptyChart();

        var points = new List<(string Name, double Guard, int Value)>();
        foreach (var variant in template.Variants)
        {
            if (variant.Zones is null) continue;
            foreach (var zone in variant.Zones)
            {
                if (!zone.GuardMultiplier.HasValue) continue;
                if (!zone.ResourcesValue.HasValue) continue;
                var name = string.IsNullOrEmpty(zone.Name) ? "(unnamed)" : zone.Name;
                points.Add((name, zone.GuardMultiplier.Value, zone.ResourcesValue.Value));
            }
        }

        if (points.Count == 0)
            return EmptyChart();

        // Outlier detection needs spread; below 4 points we cannot meaningfully
        // declare a quartile boundary, so flag nothing.
        bool[] outliers = new bool[points.Count];
        if (points.Count >= 4)
        {
            double valueP75 = Percentile(points.Select(p => (double)p.Value).ToArray(), 0.75);
            double guardP25 = Percentile(points.Select(p => p.Guard).ToArray(), 0.25);
            for (int i = 0; i < points.Count; i++)
            {
                outliers[i] = points[i].Value >= valueP75 && points[i].Guard <= guardP25;
            }
        }

        var rendered = new List<GuardChartPoint>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            rendered.Add(new GuardChartPoint(points[i].Name, points[i].Guard, points[i].Value, outliers[i]));
        }

        double gMin = points.Min(p => p.Guard);
        double gMax = points.Max(p => p.Guard);
        int vMin = points.Min(p => p.Value);
        int vMax = points.Max(p => p.Value);

        return new GuardChartReport(rendered, gMin, gMax, vMin, vMax);
    }

    /// <summary>
    /// Type-7 percentile (Excel / numpy default). The input array is mutated
    /// (sorted) — callers pass a fresh array.
    /// </summary>
    private static double Percentile(double[] values, double q)
    {
        System.Array.Sort(values);
        if (values.Length == 1) return values[0];
        double pos = (values.Length - 1) * q;
        int lo = (int)System.Math.Floor(pos);
        int hi = (int)System.Math.Ceiling(pos);
        if (lo == hi) return values[lo];
        double frac = pos - lo;
        return values[lo] + frac * (values[hi] - values[lo]);
    }

    private static GuardChartReport EmptyChart() =>
        new(System.Array.Empty<GuardChartPoint>(), 0, 1, 0, 1);
}
