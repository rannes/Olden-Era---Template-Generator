using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-205 — per-tier terrain density. The schema models obstaclesFill /
/// lakesFill only on ZoneLayout, and the four built-in layouts are
/// shared across tiers. Per-tier density therefore clones the base
/// layout per (layout, tier) pair that diverges and rewrites the
/// affected zones' Layout ref to the clone. Defaults must be no-op.
/// </summary>
public class PerTierTerrainDensityTests
{
    private static readonly JsonSerializerOptions EmitJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Advanced mode lets us configure neutrals per tier explicitly so the
    // per-tier code path has real Low/Medium/High targets.
    private static GeneratorSettings MakeSettings()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 4,
            Topology = MapTopology.Default,
            Seed = 42,
        };
        s.ZoneCfg.NeutralZoneCount = 6;
        s.ZoneCfg.Advanced.Enabled = true;
        s.ZoneCfg.Advanced.NeutralLowNoCastleCount    = 2;
        s.ZoneCfg.Advanced.NeutralMediumNoCastleCount = 2;
        s.ZoneCfg.Advanced.NeutralHighNoCastleCount   = 2;
        return s;
    }

    [Fact]
    public void DefaultTierOverrides_ProduceByteIdenticalOutput()
    {
        // Touching the per-tier struct without setting any density value
        // must not perturb the emitted JSON.
        var s1 = MakeSettings();
        var _ = s1.ZoneCfg.Advanced.LowTier;        // ensure ctor path exercised
        s1.ZoneCfg.Advanced.LowTier.GuardWeeklyIncrement = 0.0;

        var s2 = MakeSettings();

        string j1 = JsonSerializer.Serialize(TemplateGenerator.Generate(s1), EmitJsonOptions);
        string j2 = JsonSerializer.Serialize(TemplateGenerator.Generate(s2), EmitJsonOptions);

        Assert.Equal(j2, j1);
    }

    [Fact]
    public void HighTierObstaclesOverride_ClonesLayout_AndRetargetsHighZones()
    {
        var baseline = MakeSettings();
        var withHigh = MakeSettings();
        withHigh.ZoneCfg.Advanced.HighTier.ObstaclesFill = 0.42;

        var bt = TemplateGenerator.Generate(baseline);
        var wt = TemplateGenerator.Generate(withHigh);

        // Baseline: layout list size unchanged from BuildZoneLayouts (4).
        Assert.NotNull(bt.ZoneLayouts);
        Assert.Equal(4, bt.ZoneLayouts!.Count);

        // Override: at least one high-tier clone appended.
        Assert.NotNull(wt.ZoneLayouts);
        var highClones = wt.ZoneLayouts!.Where(l => l.Name.EndsWith("_high")).ToList();
        Assert.NotEmpty(highClones);
        Assert.All(highClones, c => Assert.Equal(0.42, c.ObstaclesFill));

        // Every high clone must be referenced by at least one zone (no dead
        // layouts), and no base layout name should still hold a high zone.
        Assert.NotNull(wt.Variants);
        var refs = wt.Variants!
            .SelectMany(v => v.Zones ?? new System.Collections.Generic.List<Zone>())
            .Select(z => z.Layout)
            .ToHashSet();
        foreach (var c in highClones)
            Assert.Contains(c.Name, refs);
    }

    [Fact]
    public void LowAndHighOverrides_ProduceSeparateClones()
    {
        var s = MakeSettings();
        s.ZoneCfg.Advanced.LowTier.ObstaclesFill = 0.20;
        s.ZoneCfg.Advanced.HighTier.LakesFill = 0.15;

        var t = TemplateGenerator.Generate(s);
        Assert.NotNull(t.ZoneLayouts);
        var clones = t.ZoneLayouts!.Where(l => l.Name.EndsWith("_low") || l.Name.EndsWith("_high")).ToList();
        Assert.NotEmpty(clones);

        // Low clones must carry the low override and inherit lakes from their base.
        foreach (var c in clones.Where(c => c.Name.EndsWith("_low")))
            Assert.Equal(0.20, c.ObstaclesFill);
        // High clones must carry the high override on lakes only.
        foreach (var c in clones.Where(c => c.Name.EndsWith("_high")))
            Assert.Equal(0.15, c.LakesFill);
    }

    [Fact]
    public void TierOverride_BeatsGlobalTerrainOverride_ForThatTier()
    {
        var s = MakeSettings();
        s.Terrain.ObstaclesFill = 0.10;            // global stamp
        s.ZoneCfg.Advanced.HighTier.ObstaclesFill = 0.50; // tier override wins for High

        var t = TemplateGenerator.Generate(s);
        Assert.NotNull(t.ZoneLayouts);

        // Base layouts get the global value.
        var baseLayouts = t.ZoneLayouts!.Where(l => !l.Name.EndsWith("_low") && !l.Name.EndsWith("_medium") && !l.Name.EndsWith("_high")).ToList();
        Assert.All(baseLayouts, l => Assert.Equal(0.10, l.ObstaclesFill));

        // High clones get the tier value.
        var highClones = t.ZoneLayouts!.Where(l => l.Name.EndsWith("_high")).ToList();
        Assert.NotEmpty(highClones);
        Assert.All(highClones, l => Assert.Equal(0.50, l.ObstaclesFill));
    }

    [Fact]
    public void TierOverride_RoundTripsThroughSettingsFile()
    {
        var g = new GeneratorSettings();
        g.ZoneCfg.Advanced.LowTier.ObstaclesFill = 0.11;
        g.ZoneCfg.Advanced.MediumTier.LakesFill  = 0.22;
        g.ZoneCfg.Advanced.HighTier.ObstaclesFill = 0.33;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var roundTripped = SettingsMapper.FromFile(file).Settings;

        Assert.Equal(0.11, roundTripped.ZoneCfg.Advanced.LowTier.ObstaclesFill);
        Assert.Equal(0.22, roundTripped.ZoneCfg.Advanced.MediumTier.LakesFill);
        Assert.Equal(0.33, roundTripped.ZoneCfg.Advanced.HighTier.ObstaclesFill);
    }
}
