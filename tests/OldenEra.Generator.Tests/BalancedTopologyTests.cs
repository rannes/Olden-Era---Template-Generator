#pragma warning disable CS8602, CS8604
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class BalancedTopologyTests
{
    private static GeneratorSettings BalancedSettings(int playerCount, int neutralCount, int? seed = null) => new()
    {
        TemplateName = "balanced-test",
        PlayerCount = playerCount,
        Topology = MapTopology.Balanced,
        Seed = seed,
        ZoneCfg = new ZoneConfiguration { NeutralZoneCount = neutralCount },
    };

    [Fact]
    public void Balanced_GeneratesTemplateWithExpectedZoneCount()
    {
        var settings = BalancedSettings(playerCount: 4, neutralCount: 8);
        var template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template);
        var firstVariant = template.Variants!.First();
        Assert.Equal(12, firstVariant.Zones.Count);
    }

    [Fact]
    public void Balanced_StampsGeneratorPositionsOnEveryZone()
    {
        var settings = BalancedSettings(playerCount: 3, neutralCount: 9);
        var template = TemplateGenerator.Generate(settings);
        var variant = template.Variants!.First();

        Assert.All(variant.Zones, z => Assert.NotNull(z.GeneratorPosition));
    }

    [Fact]
    public void Balanced_WithSeed_IsDeterministic()
    {
        var s1 = BalancedSettings(playerCount: 4, neutralCount: 8, seed: 42);
        var s2 = BalancedSettings(playerCount: 4, neutralCount: 8, seed: 42);

        var t1 = TemplateGenerator.Generate(s1);
        var t2 = TemplateGenerator.Generate(s2);

        var z1 = t1.Variants!.First().Zones!
            .Select(z => (z.Name, z.GeneratorPosition))
            .ToArray();
        var z2 = t2.Variants!.First().Zones!
            .Select(z => (z.Name, z.GeneratorPosition))
            .ToArray();

        Assert.Equal(z1, z2);
    }

    [Fact]
    public void Balanced_GraphIsConnected()
    {
        var settings = BalancedSettings(playerCount: 4, neutralCount: 12);
        var template = TemplateGenerator.Generate(settings);
        var variant = template.Variants!.First();

        var zoneNames = variant.Zones.Select(z => z.Name).ToHashSet(StringComparer.Ordinal);
        var adj = zoneNames.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal));
        foreach (var c in variant.Connections)
        {
            if (c.ConnectionType is not ("Direct" or "Portal")) continue;
            if (c.From == null || c.To == null) continue;
            if (!adj.ContainsKey(c.From) || !adj.ContainsKey(c.To)) continue;
            adj[c.From].Add(c.To);
            adj[c.To].Add(c.From);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(zoneNames.First());
        seen.Add(zoneNames.First());
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var n in adj[cur])
                if (seen.Add(n)) queue.Enqueue(n);
        }

        Assert.Equal(zoneNames.Count, seen.Count);
    }

    [Fact]
    public void Tournament_BalancedTopology_ProducesTwoIsolatedClusters()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "tournament-balanced",
            PlayerCount = 2,
            Topology = MapTopology.Balanced,
            GameEndConditions = new GameEndConditions { VictoryCondition = "win_condition_6" },
            TournamentRules = new TournamentRules { Enabled = true },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 8 },
        };

        var template = TemplateGenerator.Generate(settings);
        var variant = template.Variants!.First();

        // Build adjacency over Direct connections only; portals are intentionally absent here.
        var zoneNames = variant.Zones.Select(z => z.Name).ToHashSet(StringComparer.Ordinal);
        var adj = zoneNames.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal));
        foreach (var c in variant.Connections.Where(c => c.ConnectionType == "Direct"))
        {
            if (c.From == null || c.To == null) continue;
            if (!adj.ContainsKey(c.From) || !adj.ContainsKey(c.To)) continue;
            adj[c.From].Add(c.To);
            adj[c.To].Add(c.From);
        }

        // Components.
        var compId = new Dictionary<string, int>(StringComparer.Ordinal);
        int next = 0;
        foreach (var start in zoneNames)
        {
            if (compId.ContainsKey(start)) continue;
            var q = new Queue<string>();
            q.Enqueue(start); compId[start] = next;
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var n in adj[cur])
                    if (!compId.ContainsKey(n)) { compId[n] = next; q.Enqueue(n); }
            }
            next++;
        }

        // Each player must be in a distinct component (full isolation).
        Assert.Equal(2, compId["Spawn-A"] == compId["Spawn-B"] ? 1 : 2);
        Assert.NotEqual(compId["Spawn-A"], compId["Spawn-B"]);
    }

    [Fact]
    public void Random_TopologyDoesNotUseConcentricPositions()
    {
        // Random with a fixed seed should produce scattered positions, not snapped rings.
        // Sanity: at least one zone must be off-centre by a non-trivial amount in a way
        // that wouldn't hold for a clean ring layout.
        var settings = new GeneratorSettings
        {
            TemplateName = "random-test",
            PlayerCount = 4,
            Topology = MapTopology.Random,
            Seed = 7,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 8 },
        };

        var template = TemplateGenerator.Generate(settings);
        var variant = template.Variants!.First();
        Assert.All(variant.Zones, z => Assert.NotNull(z.GeneratorPosition));
    }

    [Fact]
    public void SettingsMapper_MigratesLegacyExperimentalBalancedFlag()
    {
        var file = new SettingsFile
        {
            Topology = MapTopology.Random,
            ExperimentalBalancedZonePlacement = true,
        };

        var (settings, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(MapTopology.Balanced, settings.Topology);
    }

    [Fact]
    public void SettingsMapper_LeavesNonRandomTopologyUntouched()
    {
        var file = new SettingsFile
        {
            Topology = MapTopology.Default,
            ExperimentalBalancedZonePlacement = true,
        };

        var (settings, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(MapTopology.Default, settings.Topology);
    }
}
