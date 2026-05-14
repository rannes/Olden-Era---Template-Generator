using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-004: Zone.guardReactionDistribution becomes a configurable per-template
/// curve (with presets + a custom array). Defaults must keep the existing
/// hardcoded shapes byte-for-byte so shipped snapshots stay stable.
/// </summary>
public class GuardReactionDistributionTests
{
    [Fact]
    public void DefaultSettings_PreserveLegacyPerZoneArrays()
    {
        var s = new GeneratorSettings
        {
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 1,
                NeutralZoneCastles = 1,
                HubZoneSize = 1.0,
                HubZoneCastles = 1,
            },
        };
        // Force a hub-and-spoke layout so all three zone shapes (Hub, Neutral, Spawn) appear.
        s.Topology = MapTopology.HubAndSpoke;

        var template = TemplateGenerator.Generate(s);
        var zones = template.Variants![0].Zones!;

        // Spawn zones: legacy front-loaded array.
        var spawn = zones.Find(z => z.Name!.StartsWith("Spawn-"));
        Assert.NotNull(spawn);
        Assert.Equal(new[] { 60, 20, 10, 10, 2, 0 }, spawn!.GuardReactionDistribution);

        // Neutral zones: legacy quality-dependent array. Default plan creates
        // medium-quality neutral zones, so the non-High shape applies.
        var neutral = zones.Find(z => z.Name!.StartsWith("Neutral-"));
        if (neutral is not null)
        {
            Assert.Equal(6, neutral.GuardReactionDistribution!.Count);
            Assert.True(
                neutral.GuardReactionDistribution.SequenceEqual(new[] { 0, 10, 10, 20, 10, 0 })
                || neutral.GuardReactionDistribution.SequenceEqual(new[] { 0, 10, 10, 10, 10, 0 }),
                $"Unexpected default neutral curve: [{string.Join(",", neutral.GuardReactionDistribution)}]");
        }

        // Hub zone: legacy hub array.
        var hub = zones.Find(z => z.Name == "Hub");
        Assert.NotNull(hub);
        Assert.Equal(new[] { 0, 10, 10, 20, 10, 0 }, hub!.GuardReactionDistribution);
    }

    [Fact]
    public void FrontLoadedPreset_OverridesEveryZone()
    {
        var s = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings { Preset = GuardReactionPreset.FrontLoaded },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 2, NeutralZoneCastles = 1 },
        };

        var template = TemplateGenerator.Generate(s);
        var expected = new[] { 60, 20, 10, 10, 2, 0 };
        var zones = template.Variants![0].Zones!;
        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(expected, z.GuardReactionDistribution));
    }

    [Fact]
    public void EvenPreset_StampsUniformCurve()
    {
        var s = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings { Preset = GuardReactionPreset.Even },
        };
        var template = TemplateGenerator.Generate(s);
        var expected = new[] { 10, 10, 10, 10, 10, 10 };
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(expected, z.GuardReactionDistribution));
    }

    [Fact]
    public void BackLoadedPreset_StampsRisingCurve()
    {
        var s = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings { Preset = GuardReactionPreset.BackLoaded },
        };
        var template = TemplateGenerator.Generate(s);
        var expected = new[] { 0, 2, 10, 20, 30, 40 };
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(expected, z.GuardReactionDistribution));
    }

    [Fact]
    public void CustomPreset_EmitsArrayVerbatimWhenLengthSix()
    {
        var custom = new List<int> { 5, 4, 3, 2, 1, 0 };
        var s = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings
            {
                Preset = GuardReactionPreset.Custom,
                CustomDistribution = custom,
            },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(custom, z.GuardReactionDistribution));
    }

    [Fact]
    public void CustomPreset_FallsBackToDefaultsWhenLengthIsWrong()
    {
        // Wrong length → ignored, behave as Default.
        var s = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings
            {
                Preset = GuardReactionPreset.Custom,
                CustomDistribution = new List<int> { 1, 2, 3 }, // length 3, not 6
            },
        };
        var template = TemplateGenerator.Generate(s);
        var spawn = template.Variants![0].Zones!.Find(z => z.Name!.StartsWith("Spawn-"));
        Assert.NotNull(spawn);
        Assert.Equal(new[] { 60, 20, 10, 10, 2, 0 }, spawn!.GuardReactionDistribution);
    }

    [Fact]
    public void SettingsMapper_RoundTripsGuardReaction()
    {
        var g = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings
            {
                Preset = GuardReactionPreset.Custom,
                CustomDistribution = new List<int> { 1, 1, 4, 4, 2, 1 },
            },
        };
        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        Assert.Equal("Custom", file.GuardReactionPreset);
        Assert.Equal("1,1,4,4,2,1", file.GuardReactionCustomDistribution);

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Equal(GuardReactionPreset.Custom, back.GuardReaction.Preset);
        Assert.Equal(new[] { 1, 1, 4, 4, 2, 1 }, back.GuardReaction.CustomDistribution);
    }

    [Fact]
    public void SettingsMapper_DefaultGuardReactionEmitsEmptyStrings()
    {
        var g = new GeneratorSettings();
        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        Assert.Equal("", file.GuardReactionPreset);
        Assert.Equal("", file.GuardReactionCustomDistribution);
    }

    [Fact]
    public void SettingsMapper_RoundTripsFrontLoadedPreset()
    {
        var g = new GeneratorSettings
        {
            GuardReaction = new GuardReactionSettings { Preset = GuardReactionPreset.FrontLoaded },
        };
        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        Assert.Equal("FrontLoaded", file.GuardReactionPreset);

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Equal(GuardReactionPreset.FrontLoaded, back.GuardReaction.Preset);
    }
}
