using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-704 — verify <see cref="TemplateAnalysis.ComputeFairness"/> reads player
/// zones (Spawn-bearing zones) off the emitted template, summarises
/// neighbour count / starting-castle count / resource yield, and flags rows
/// whose any metric deviates by more than 20% of the median.
/// </summary>
public class TemplateAnalysisFairnessTests
{
    private static GeneratorSettings BalancedSettings(int seed = 1234) => new()
    {
        TemplateName = "T-704 balanced",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Random,
        Seed = seed,
    };

    [Fact]
    public void NullTemplate_ReturnsEmptyReport()
    {
        var report = TemplateAnalysis.ComputeFairness(null);

        Assert.NotNull(report);
        Assert.False(report.HasData);
        Assert.False(report.AnyOutlier);
        Assert.Empty(report.Players);
    }

    [Fact]
    public void TemplateWithoutSpawns_ReturnsEmptyReport()
    {
        var template = new RmgTemplate
        {
            Variants = new()
            {
                new Variant
                {
                    Zones = new() { new Zone { Name = "n1" } },
                },
            },
        };

        var report = TemplateAnalysis.ComputeFairness(template);
        Assert.False(report.HasData);
    }

    [Fact]
    public void BalancedPreset_AllPlayersPassClean()
    {
        // Default 4-player Random preset is mirrored by construction —
        // every player-zone derives from the same tuning profile, so the
        // fairness audit must not flag anyone.
        var template = TemplateGenerator.Generate(BalancedSettings());

        var report = TemplateAnalysis.ComputeFairness(template);

        Assert.True(report.HasData);
        Assert.Equal(4, report.Players.Count);
        Assert.False(
            report.AnyOutlier,
            $"Balanced preset must pass clean. Outliers: " +
            string.Join(
                ", ",
                report.Players.Where(p => p.IsOutlier).Select(p => $"P{p.Slot}")));
    }

    [Fact]
    public void BalancedPresets_PassCleanAcrossMirroredTopologies()
    {
        // Default / SharedWeb / Balanced / Random / Chain are mirrored by
        // construction — every player-zone derives from the same tuning
        // profile, so the fairness audit must not flag anyone. HubAndSpoke
        // is excluded because the hub zone is intentionally asymmetric.
        foreach (var topo in new[]
        {
            MapTopology.Default,
            MapTopology.SharedWeb,
            MapTopology.Balanced,
            MapTopology.Random,
        })
        {
            var settings = BalancedSettings();
            settings.Topology = topo;
            var template = TemplateGenerator.Generate(settings);

            var report = TemplateAnalysis.ComputeFairness(template);

            Assert.True(report.HasData, $"Topology {topo}: expected player rows.");
            Assert.False(
                report.AnyOutlier,
                $"Topology {topo}: expected no outliers. Got: " +
                string.Join(
                    ", ",
                    report.Players.Where(p => p.IsOutlier).Select(p => $"P{p.Slot}")));
        }
    }

    [Fact]
    public void AsymmetricFixture_ResourceYieldOutlierLightsUp()
    {
        // P1 gets 10x the resource yield of P2/P3/P4 → must flag.
        var template = BuildPlayerFixture(new[]
        {
            new PlayerSpec(1, neighbors: 2, castles: 1, resourceValue: 10000),
            new PlayerSpec(2, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(3, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(4, neighbors: 2, castles: 1, resourceValue: 1000),
        });

        var report = TemplateAnalysis.ComputeFairness(template);

        Assert.True(report.HasData);
        var p1 = report.Players.Single(p => p.Slot == 1);
        Assert.True(p1.ResourceYieldOutlier);
        Assert.True(p1.IsOutlier);
        // The well-funded player skews the median upward but the others
        // must still pass — they sit at the median.
        Assert.All(
            report.Players.Where(p => p.Slot != 1),
            p => Assert.False(p.IsOutlier));
    }

    [Fact]
    public void AsymmetricFixture_CastleCountOutlierLightsUp()
    {
        // P1 has 3 cities, others have 1 → starting-castle outlier.
        var template = BuildPlayerFixture(new[]
        {
            new PlayerSpec(1, neighbors: 2, castles: 3, resourceValue: 1000),
            new PlayerSpec(2, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(3, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(4, neighbors: 2, castles: 1, resourceValue: 1000),
        });

        var report = TemplateAnalysis.ComputeFairness(template);

        var p1 = report.Players.Single(p => p.Slot == 1);
        Assert.True(p1.StartingCastleOutlier);
        Assert.True(p1.IsOutlier);
    }

    [Fact]
    public void AsymmetricFixture_NeighborOutlierLightsUp()
    {
        // P1 connects to everyone, P2-P4 each connect only to P1 → P1
        // sees 3 neighbours vs the median 1.
        var zones = new[] { "Spawn-A", "Spawn-B", "Spawn-C", "Spawn-D" };
        var connections = new[]
        {
            ("Spawn-A", "Spawn-B"),
            ("Spawn-A", "Spawn-C"),
            ("Spawn-A", "Spawn-D"),
        };
        var template = BuildSpawnTopologyFixture(zones, connections);

        var report = TemplateAnalysis.ComputeFairness(template);

        var p1 = report.Players.Single(p => p.Slot == 1);
        Assert.Equal(3, p1.NeighborCount);
        Assert.True(p1.NeighborOutlier);
    }

    [Fact]
    public void Bonuses_ReceiverSide_TallyDifferentiatesYield()
    {
        // Same zone yield across 4 players, but P1 receives 5 bonus entries.
        var template = BuildPlayerFixture(new[]
        {
            new PlayerSpec(1, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(2, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(3, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(4, neighbors: 2, castles: 1, resourceValue: 1000),
        });
        // Median yield across 4 zones is 1000; +5 on slot 1 only is below
        // the 20% tolerance (200), so we add enough bonuses to trip it.
        template.GameRules = new GameRules
        {
            Bonuses = Enumerable.Range(0, 250)
                .Select(_ => new Bonus { Sid = "add_bonus_res", ReceiverSide = 1 })
                .ToList(),
        };

        var report = TemplateAnalysis.ComputeFairness(template);

        var p1 = report.Players.Single(p => p.Slot == 1);
        Assert.True(p1.ResourceYieldOutlier);
        Assert.Equal(1250, p1.ResourceYield);
    }

    [Fact]
    public void DeviationThresholdHonoursParameter()
    {
        // Build a fixture where P1's yield is +25% over the others — under
        // the default 20% threshold this flags, under a 30% threshold it
        // does not. Pins the parameter wiring.
        var template = BuildPlayerFixture(new[]
        {
            new PlayerSpec(1, neighbors: 2, castles: 1, resourceValue: 1250),
            new PlayerSpec(2, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(3, neighbors: 2, castles: 1, resourceValue: 1000),
            new PlayerSpec(4, neighbors: 2, castles: 1, resourceValue: 1000),
        });

        var defaultReport = TemplateAnalysis.ComputeFairness(template);
        var loose = TemplateAnalysis.ComputeFairness(template, deviationPercent: 30);

        Assert.True(defaultReport.Players.Single(p => p.Slot == 1).ResourceYieldOutlier);
        Assert.False(loose.Players.Single(p => p.Slot == 1).ResourceYieldOutlier);
    }

    // -- fixtures --------------------------------------------------------

    private sealed record PlayerSpec(
        int Slot, int neighbors, int castles, int resourceValue);

    /// <summary>
    /// Build a synthetic single-variant template with one Spawn zone per
    /// PlayerSpec. Connections are added so each player has exactly the
    /// requested neighbour count by linking each Spawn-N to N+1, N+2, ...
    /// distinct neutral zones unique to that player.
    /// </summary>
    private static RmgTemplate BuildPlayerFixture(PlayerSpec[] specs)
    {
        var zones = new List<Zone>();
        var connections = new List<Connection>();

        foreach (var spec in specs)
        {
            string zoneName = $"Spawn-{spec.Slot}";
            var mainObjects = new List<MainObject>
            {
                new MainObject { Type = "Spawn", Spawn = $"Player{spec.Slot}" },
            };
            for (int c = 0; c < spec.castles; c++)
                mainObjects.Add(new MainObject { Type = "City" });

            zones.Add(new Zone
            {
                Name = zoneName,
                MainObjects = mainObjects,
                ResourcesValue = spec.resourceValue,
            });

            for (int n = 0; n < spec.neighbors; n++)
            {
                string neutralName = $"N-{spec.Slot}-{n}";
                zones.Add(new Zone { Name = neutralName });
                connections.Add(new Connection { From = zoneName, To = neutralName });
            }
        }

        return new RmgTemplate
        {
            Name = "fairness-fixture",
            Variants = new()
            {
                new Variant { Zones = zones, Connections = connections },
            },
        };
    }

    /// <summary>
    /// Build a fixture where caller controls the connection topology
    /// directly. Used for the neighbour-outlier test.
    /// </summary>
    private static RmgTemplate BuildSpawnTopologyFixture(
        string[] spawnZoneNames,
        (string from, string to)[] edges)
    {
        var zones = new List<Zone>();
        for (int i = 0; i < spawnZoneNames.Length; i++)
        {
            zones.Add(new Zone
            {
                Name = spawnZoneNames[i],
                MainObjects = new()
                {
                    new MainObject { Type = "Spawn", Spawn = $"Player{i + 1}" },
                    new MainObject { Type = "City" },
                },
                ResourcesValue = 1000,
            });
        }

        return new RmgTemplate
        {
            Name = "topology-fixture",
            Variants = new()
            {
                new Variant
                {
                    Zones = zones,
                    Connections = edges
                        .Select(e => new Connection { From = e.from, To = e.to })
                        .ToList(),
                },
            },
        };
    }
}
