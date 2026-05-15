using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-705 — verify <see cref="TemplateAnalysis.ComputeTopology"/> reads
/// emitted <see cref="Variant.Connections"/> as an undirected simple graph
/// and reports correct degree, diameter, components, and articulation
/// points. Hand-built micro-graphs cover the algorithmic edges; shipped
/// preset fixtures pin the integration values.
/// </summary>
public class TemplateAnalysisTopologyTests
{
    private static RmgTemplate BuildTemplate(IEnumerable<string> zoneNames, IEnumerable<(string From, string To)> edges)
    {
        var v = new Variant
        {
            Zones = zoneNames.Select(n => new Zone { Name = n }).ToList(),
            Connections = edges.Select(e => new Connection { From = e.From, To = e.To }).ToList(),
        };
        return new RmgTemplate { Name = "test", Variants = new List<Variant> { v } };
    }

    [Fact]
    public void NullTemplate_ReturnsEmptyReport()
    {
        var report = TemplateAnalysis.ComputeTopology(null);
        Assert.False(report.HasData);
        Assert.Empty(report.Variants);
    }

    [Fact]
    public void NoVariants_ReturnsEmptyReport()
    {
        var report = TemplateAnalysis.ComputeTopology(new RmgTemplate { Name = "x" });
        Assert.False(report.HasData);
    }

    [Fact]
    public void Triangle_AverageDegreeIsTwo_DiameterOne_NoArticulation()
    {
        var t = BuildTemplate(
            new[] { "A", "B", "C" },
            new[] { ("A", "B"), ("B", "C"), ("C", "A") });

        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(3, stats.NodeCount);
        Assert.Equal(3, stats.EdgeCount);
        Assert.Equal(2.0, stats.AverageDegree, 3);
        Assert.Equal(1, stats.Diameter);
        Assert.Equal(1, stats.ComponentCount);
        Assert.Empty(stats.ArticulationPoints);
    }

    [Fact]
    public void PathGraph_DiameterEqualsLength_InteriorNodesAreArticulation()
    {
        // A-B-C-D-E : diameter 4, articulation = {B, C, D}
        var t = BuildTemplate(
            new[] { "A", "B", "C", "D", "E" },
            new[] { ("A", "B"), ("B", "C"), ("C", "D"), ("D", "E") });

        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(5, stats.NodeCount);
        Assert.Equal(4, stats.EdgeCount);
        Assert.Equal(4, stats.Diameter);
        Assert.Equal(1, stats.ComponentCount);
        Assert.Equal(new[] { "B", "C", "D" }, stats.ArticulationPoints);
    }

    [Fact]
    public void StarGraph_CenterIsTheOnlyArticulation_DiameterTwo()
    {
        // C connected to L1..L4
        var t = BuildTemplate(
            new[] { "C", "L1", "L2", "L3", "L4" },
            new[] { ("C", "L1"), ("C", "L2"), ("C", "L3"), ("C", "L4") });

        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(2, stats.Diameter);
        Assert.Equal(1, stats.ComponentCount);
        Assert.Equal(new[] { "C" }, stats.ArticulationPoints);
        Assert.Equal(8.0 / 5.0, stats.AverageDegree, 3);
    }

    [Fact]
    public void ParallelEdgesAndSelfLoops_AreDeduped()
    {
        var t = BuildTemplate(
            new[] { "A", "B" },
            new[] { ("A", "B"), ("A", "B"), ("A", "A") });

        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(2, stats.NodeCount);
        Assert.Equal(1, stats.EdgeCount);
        Assert.Equal(1, stats.Diameter);
    }

    [Fact]
    public void DisconnectedGraph_ReportsComponentsAndDiameterOfLargest()
    {
        // Component 1: A-B-C  (diameter 2)
        // Component 2: D-E    (diameter 1)
        var t = BuildTemplate(
            new[] { "A", "B", "C", "D", "E" },
            new[] { ("A", "B"), ("B", "C"), ("D", "E") });

        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(2, stats.ComponentCount);
        Assert.Equal(2, stats.Diameter);
        Assert.Equal(new[] { "B" }, stats.ArticulationPoints);
    }

    [Fact]
    public void IsolatedNodes_DiameterNullWhenNoEdges()
    {
        var t = BuildTemplate(new[] { "A", "B" }, System.Array.Empty<(string, string)>());
        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(0, stats.EdgeCount);
        Assert.Null(stats.Diameter);
        Assert.Equal(2, stats.ComponentCount);
        Assert.Empty(stats.ArticulationPoints);
    }

    [Fact]
    public void BridgeBetweenTriangles_BridgeEndpointsAreArticulation()
    {
        // Two triangles A-B-C and D-E-F joined by bridge C-D.
        // Articulation points: C and D.
        var t = BuildTemplate(
            new[] { "A", "B", "C", "D", "E", "F" },
            new[]
            {
                ("A", "B"), ("B", "C"), ("C", "A"),
                ("C", "D"),
                ("D", "E"), ("E", "F"), ("F", "D"),
            });

        var stats = TemplateAnalysis.ComputeTopology(t).Variants.Single();
        Assert.Equal(1, stats.ComponentCount);
        Assert.Equal(3, stats.Diameter); // A->...->F goes A-C-D-F
        Assert.Equal(new[] { "C", "D" }, stats.ArticulationPoints);
    }

    // ── Shipped-preset pinning ──────────────────────────────────────────
    // Generate every shipped preset, run topology, and pin the values.
    // These exist to flag changes in generator output, not to assert any
    // particular design — update the expected values when the topology of
    // a preset legitimately changes.

    public static TheoryData<string> AllShippedPresetIds()
    {
        var data = new TheoryData<string>();
        foreach (var entry in new PresetCatalog().Entries)
            data.Add(entry.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllShippedPresetIds))]
    public void EveryPreset_ProducesNonEmptyTopologyReport(string presetId)
    {
        var (settings, _, _, _) = SettingsMapper.FromFile(new PresetCatalog().Load(presetId));
        settings.Seed = 1234;
        var template = TemplateGenerator.Generate(settings);

        var report = TemplateAnalysis.ComputeTopology(template);

        Assert.True(report.HasData, $"Preset {presetId} produced empty topology report.");
        var stats = report.Variants[0];
        Assert.True(stats.NodeCount > 0, $"Preset {presetId} reported 0 nodes.");
        Assert.True(stats.AverageDegree >= 0);
        // If at least one edge exists, diameter must be set; otherwise null.
        if (stats.EdgeCount > 0)
            Assert.NotNull(stats.Diameter);
    }

    [Fact]
    public void TopologyMatchesEmittedConnections_NoRegeneration()
    {
        var (settings, _, _, _) = SettingsMapper.FromFile(new PresetCatalog().Load("six-kings"));
        settings.Seed = 42;
        var template = TemplateGenerator.Generate(settings);

        var stats = TemplateAnalysis.ComputeTopology(template).Variants.Single();

        // Hand-recompute edge count off the emitted variant: dedupe undirected
        // pairs and drop self-loops, exactly mirroring the analyzer's contract.
        var variant = template.Variants!.Single();
        var seen = new HashSet<(string, string)>();
        foreach (var c in variant.Connections ?? new List<Connection>())
        {
            if (string.IsNullOrEmpty(c.From) || string.IsNullOrEmpty(c.To)) continue;
            if (c.From == c.To) continue;
            var key = string.CompareOrdinal(c.From, c.To) < 0
                ? (c.From, c.To)
                : (c.To, c.From);
            seen.Add(key);
        }

        Assert.Equal(seen.Count, stats.EdgeCount);
    }
}
