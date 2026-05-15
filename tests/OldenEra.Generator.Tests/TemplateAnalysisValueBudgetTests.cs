using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-701 — verify <see cref="TemplateAnalysis.ComputeValueBudget"/> reads exactly
/// what the generator emits onto each <see cref="Zone"/>, and that totals match
/// a hand-summed reference against a known preset.
/// </summary>
public class TemplateAnalysisValueBudgetTests
{
    private static GeneratorSettings BaseSettings(int seed = 1234) => new()
    {
        TemplateName = "T-701 Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Random,
        Seed = seed,
    };

    [Fact]
    public void NullTemplate_ReturnsEmptyReport()
    {
        var report = TemplateAnalysis.ComputeValueBudget(null);

        Assert.NotNull(report);
        Assert.False(report.HasData);
        Assert.Empty(report.Zones);
        Assert.Equal(0, report.Totals.Combined);
    }

    [Fact]
    public void TemplateWithNoVariants_ReturnsEmptyReport()
    {
        var template = new RmgTemplate { Name = "blank" };

        var report = TemplateAnalysis.ComputeValueBudget(template);

        Assert.False(report.HasData);
        Assert.Empty(report.Zones);
    }

    [Fact]
    public void GeneratedTemplate_ZoneRowsMatchEmittedFields()
    {
        var template = TemplateGenerator.Generate(BaseSettings());

        var report = TemplateAnalysis.ComputeValueBudget(template);

        Assert.True(report.HasData);

        // Cross-check every row against the source-of-truth zone object.
        var emittedZones = template.Variants!
            .Where(v => v.Zones is not null)
            .SelectMany(v => v.Zones!)
            .ToList();

        Assert.Equal(emittedZones.Count, report.Zones.Count);

        for (int i = 0; i < emittedZones.Count; i++)
        {
            var z = emittedZones[i];
            var row = report.Zones[i];
            Assert.Equal(z.Name, row.ZoneName);
            Assert.Equal(z.ResourcesValue, row.ResourcesValue);
            Assert.Equal(z.ResourcesValuePerArea, row.ResourcesValuePerArea);
            Assert.Equal(z.GuardedContentValue, row.GuardedContentValue);
            Assert.Equal(z.GuardedContentValuePerArea, row.GuardedContentValuePerArea);
            Assert.Equal(z.UnguardedContentValue, row.UnguardedContentValue);
            Assert.Equal(z.UnguardedContentValuePerArea, row.UnguardedContentValuePerArea);
        }
    }

    [Fact]
    public void Totals_AreSumOfScalarColumnsOnly()
    {
        var template = TemplateGenerator.Generate(BaseSettings());
        var emittedZones = template.Variants!
            .Where(v => v.Zones is not null)
            .SelectMany(v => v.Zones!)
            .ToList();

        var report = TemplateAnalysis.ComputeValueBudget(template);

        int expectedResources = emittedZones.Sum(z => z.ResourcesValue ?? 0);
        int expectedGuarded = emittedZones.Sum(z => z.GuardedContentValue ?? 0);
        int expectedUnguarded = emittedZones.Sum(z => z.UnguardedContentValue ?? 0);

        Assert.Equal(expectedResources, report.Totals.ResourcesValue);
        Assert.Equal(expectedGuarded, report.Totals.GuardedContentValue);
        Assert.Equal(expectedUnguarded, report.Totals.UnguardedContentValue);
        Assert.Equal(
            expectedResources + expectedGuarded + expectedUnguarded,
            report.Totals.Combined);
    }

    [Fact]
    public void NullValueFields_TreatedAsZeroInTotals_AndPreservedInRow()
    {
        // Synthetic template with mixed null/non-null fields exercises the
        // ?? 0 fallback in totals while preserving null in the per-zone row.
        var template = new RmgTemplate
        {
            Name = "synth",
            Variants = new()
            {
                new Variant
                {
                    Zones = new()
                    {
                        new Zone
                        {
                            Name = "z1",
                            ResourcesValue = 100,
                            // GuardedContentValue intentionally null
                            UnguardedContentValue = 50,
                            ResourcesValuePerArea = 7,
                        },
                        new Zone
                        {
                            Name = "z2",
                            ResourcesValue = 200,
                            GuardedContentValue = 300,
                            // UnguardedContentValue intentionally null
                        },
                    },
                },
            },
        };

        var report = TemplateAnalysis.ComputeValueBudget(template);

        Assert.Equal(2, report.Zones.Count);
        Assert.Null(report.Zones[0].GuardedContentValue);
        Assert.Equal(7, report.Zones[0].ResourcesValuePerArea);
        Assert.Null(report.Zones[1].UnguardedContentValue);

        Assert.Equal(300, report.Totals.ResourcesValue);   // 100 + 200
        Assert.Equal(300, report.Totals.GuardedContentValue); // 0 + 300
        Assert.Equal(50, report.Totals.UnguardedContentValue); // 50 + 0
        Assert.Equal(650, report.Totals.Combined);
    }

    [Fact]
    public void EmptyZoneName_FallsBackToPlaceholder()
    {
        var template = new RmgTemplate
        {
            Variants = new()
            {
                new Variant { Zones = new() { new Zone { Name = "" } } },
            },
        };

        var report = TemplateAnalysis.ComputeValueBudget(template);

        Assert.Single(report.Zones);
        Assert.Equal("(unnamed)", report.Zones[0].ZoneName);
    }
}
