using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-702 — verify <see cref="TemplateAnalysis.ComputeGuardChart"/> reads the
/// emitted per-zone guard multiplier + resources value, and that the outlier
/// rule (value &gt;= P75 AND guard &lt;= P25) flags the rich-and-weak corner
/// without false-positiving on the rest.
/// </summary>
public class TemplateAnalysisGuardChartTests
{
    private static GeneratorSettings BaseSettings(int seed = 4242) => new()
    {
        TemplateName = "T-702 Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Random,
        Seed = seed,
    };

    private static Zone Z(string name, double guard, int value) => new()
    {
        Name = name,
        GuardMultiplier = guard,
        ResourcesValue = value,
    };

    private static RmgTemplate Wrap(params Zone[] zones) => new()
    {
        Variants = new() { new Variant { Zones = zones.ToList() } },
    };

    [Fact]
    public void NullTemplate_ReturnsEmptyReport()
    {
        var report = TemplateAnalysis.ComputeGuardChart(null);

        Assert.NotNull(report);
        Assert.False(report.HasData);
        Assert.Empty(report.Points);
        Assert.Equal(0, report.OutlierCount);
    }

    [Fact]
    public void NoVariants_ReturnsEmptyReport()
    {
        var template = new RmgTemplate { Name = "blank" };

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.False(report.HasData);
    }

    [Fact]
    public void ZonesMissingEitherField_AreExcluded()
    {
        var template = Wrap(
            new Zone { Name = "no-guard", ResourcesValue = 100 },
            new Zone { Name = "no-value", GuardMultiplier = 1.5 },
            Z("plotted", 1.0, 50));

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.Single(report.Points);
        Assert.Equal("plotted", report.Points[0].ZoneName);
    }

    [Fact]
    public void FewerThanFourPoints_FlagsNothing()
    {
        // 3 points: even if one is "rich and weak" we have insufficient spread.
        var template = Wrap(
            Z("a", 0.5, 1000),
            Z("b", 2.0, 100),
            Z("c", 1.5, 200));

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.Equal(3, report.Points.Count);
        Assert.Equal(0, report.OutlierCount);
    }

    [Fact]
    public void RichZoneWithWeakGuards_IsFlaggedAsOutlier()
    {
        // Five zones; "rich-weak" is at value=1000 (top of range) AND
        // guard=0.5 (bottom of range). Others sit in the middle.
        var template = Wrap(
            Z("rich-weak", 0.5, 1000),
            Z("balanced-1", 1.0, 200),
            Z("balanced-2", 1.0, 300),
            Z("balanced-3", 1.2, 400),
            Z("strong-poor", 2.0, 100));

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.True(report.HasData);
        var flagged = report.Points.Where(p => p.IsOutlier).ToList();
        Assert.Single(flagged);
        Assert.Equal("rich-weak", flagged[0].ZoneName);
    }

    [Fact]
    public void RichZoneWithStrongGuards_IsNotFlagged()
    {
        var template = Wrap(
            Z("rich-strong", 2.5, 1000),
            Z("mid-1", 1.0, 200),
            Z("mid-2", 1.0, 300),
            Z("mid-3", 1.2, 400),
            Z("poor-weak", 0.5, 50));

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.Equal(0, report.OutlierCount);
    }

    [Fact]
    public void AxisBounds_TrackMinAndMax()
    {
        var template = Wrap(
            Z("a", 0.5, 100),
            Z("b", 1.0, 200),
            Z("c", 1.5, 300),
            Z("d", 2.0, 400));

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.Equal(0.5, report.GuardMultiplierMin);
        Assert.Equal(2.0, report.GuardMultiplierMax);
        Assert.Equal(100, report.ResourcesValueMin);
        Assert.Equal(400, report.ResourcesValueMax);
    }

    [Fact]
    public void EmptyZoneName_FallsBackToPlaceholder()
    {
        var template = Wrap(Z("", 1.0, 100));

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.Single(report.Points);
        Assert.Equal("(unnamed)", report.Points[0].ZoneName);
    }

    [Fact]
    public void GeneratedPreset_RendersWithoutThrowingAndPointsMatchEmittedZones()
    {
        var template = TemplateGenerator.Generate(BaseSettings());

        var report = TemplateAnalysis.ComputeGuardChart(template);

        Assert.True(report.HasData);

        var emitted = template.Variants!
            .Where(v => v.Zones is not null)
            .SelectMany(v => v.Zones!)
            .Where(z => z.GuardMultiplier.HasValue && z.ResourcesValue.HasValue)
            .ToList();

        Assert.Equal(emitted.Count, report.Points.Count);
        // Ordering preserves emission order; cross-check on (guard, value).
        for (int i = 0; i < emitted.Count; i++)
        {
            Assert.Equal(emitted[i].GuardMultiplier!.Value, report.Points[i].GuardMultiplier);
            Assert.Equal(emitted[i].ResourcesValue!.Value, report.Points[i].ResourcesValue);
        }
    }
}
