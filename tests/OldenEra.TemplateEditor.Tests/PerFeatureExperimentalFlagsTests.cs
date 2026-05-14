using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Tests;

/// <summary>
/// T-302 — verifies the master <c>experimentalEnabled</c> flag has been split
/// into per-feature flags, with auto-migration for legacy .oetgs files and
/// scalar round-trip across <see cref="SettingsMapper"/> +
/// <see cref="SettingsShareCodec"/>.
/// </summary>
public class PerFeatureExperimentalFlagsTests
{
    [Fact]
    public void LegacyMasterFlag_MigratesToAllPerFeatureFlagsTrue()
    {
        // Pre-T-302 .oetgs shape: master flag on, per-feature flags absent
        // (their default is false).
        var legacyJson = """
        {
            "templateName": "Custom",
            "experimentalEnabled": true
        }
        """;
        var loaded = JsonSerializer.Deserialize<SettingsFile>(legacyJson)!;
        Assert.True(loaded.ExperimentalEnabled);
        Assert.False(loaded.ExpFeatureGameMode);
        Assert.False(loaded.ExpFeatureStartingBonuses);
        Assert.False(loaded.ExpFeatureZoneContent);
        Assert.False(loaded.ExpFeatureBordersRoads);
        Assert.False(loaded.ExpFeaturePerTierOverrides);

        var (_, _, _, flags) = SettingsMapper.FromFile(loaded);
        Assert.True(flags.GameMode);
        Assert.True(flags.StartingBonuses);
        Assert.True(flags.ZoneContent);
        Assert.True(flags.BordersRoads);
        Assert.True(flags.PerTierOverrides);
    }

    [Fact]
    public void LegacyMasterFlag_OffMeansAllPerFeatureFlagsOff()
    {
        var file = new SettingsFile { ExperimentalEnabled = false };
        var (_, _, _, flags) = SettingsMapper.FromFile(file);
        Assert.False(flags.AnyEnabled);
    }

    [Fact]
    public void PerFeatureFlags_TakePrecedenceOverLegacyMaster()
    {
        // If a file was saved by a T-302 client, both master and per-feature
        // flags are present. Per-feature must win — legacy migration applies
        // only when no per-feature flag is set.
        var file = new SettingsFile
        {
            ExperimentalEnabled = true,
            ExpFeatureGameMode = true,
            // others remain false → migration must NOT light them up.
        };
        var (_, _, _, flags) = SettingsMapper.FromFile(file);
        Assert.True(flags.GameMode);
        Assert.False(flags.StartingBonuses);
        Assert.False(flags.ZoneContent);
        Assert.False(flags.BordersRoads);
        Assert.False(flags.PerTierOverrides);
    }

    [Theory]
    [InlineData(ExperimentalFeatures.GameMode)]
    [InlineData(ExperimentalFeatures.StartingBonuses)]
    [InlineData(ExperimentalFeatures.ZoneContent)]
    [InlineData(ExperimentalFeatures.BordersRoads)]
    [InlineData(ExperimentalFeatures.PerTierOverrides)]
    public void PerFeatureFlag_RoundTripsThroughSettingsFile(string key)
    {
        var flags = new ExperimentalFlags();
        flags.Set(key, true);

        var file = SettingsMapper.ToFile(new GeneratorSettings(),
            advancedMode: false, experimentalMapSizes: false, experimental: flags);

        // Master flag derived from "any per-feature on" so older readers still
        // see the experimental nav.
        Assert.True(file.ExperimentalEnabled);

        // Round-trip via JSON to lock in the persistence contract.
        string json = JsonSerializer.Serialize(file);
        var loaded = JsonSerializer.Deserialize<SettingsFile>(json)!;
        var (_, _, _, restored) = SettingsMapper.FromFile(loaded);

        Assert.True(restored.Get(key));
        // Other flags stay off.
        foreach (var feature in ExperimentalFeatures.All)
        {
            if (feature.Key == key) continue;
            Assert.False(restored.Get(feature.Key));
        }
    }

    [Fact]
    public void AllPerFeatureFlags_RoundTripIndependently()
    {
        // Mixed pattern: 3 of 5 on. Ensures every flag is wired through ToFile
        // / FromFile, not aliased.
        var flags = new ExperimentalFlags
        {
            GameMode = true,
            ZoneContent = true,
            PerTierOverrides = true,
        };
        var file = SettingsMapper.ToFile(new GeneratorSettings(),
            advancedMode: false, experimentalMapSizes: false, experimental: flags);
        var (_, _, _, restored) = SettingsMapper.FromFile(file);

        Assert.True(restored.GameMode);
        Assert.False(restored.StartingBonuses);
        Assert.True(restored.ZoneContent);
        Assert.False(restored.BordersRoads);
        Assert.True(restored.PerTierOverrides);
    }

    [Fact]
    public void PerFeatureFlags_RoundTripThroughShareCodec()
    {
        // SettingsShareCodec.CopyNonDefault relies on every persisted field
        // being a value type / string. Plain bools satisfy that, so per-feature
        // flags must survive an encode/decode cycle.
        var file = new SettingsFile
        {
            ExpFeatureGameMode = true,
            ExpFeatureBordersRoads = true,
            ExperimentalEnabled = true,
        };
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.True(decoded!.ExpFeatureGameMode);
        Assert.True(decoded.ExpFeatureBordersRoads);
        Assert.False(decoded.ExpFeatureStartingBonuses);
        Assert.False(decoded.ExpFeatureZoneContent);
        Assert.False(decoded.ExpFeaturePerTierOverrides);
    }

    [Fact]
    public void ExperimentalFeatures_Registry_HasFiveEntries_AllExperimental()
    {
        // Locks in the initial split. When a feature graduates, this test must
        // be updated together with the registry to keep the change explicit.
        Assert.Equal(5, ExperimentalFeatures.All.Count);
        Assert.All(ExperimentalFeatures.All,
            f => Assert.Equal(ExperimentalStatus.Experimental, f.Status));

        // Stable keys — UI templates rely on these.
        var keys = ExperimentalFeatures.All.Select(f => f.Key).ToArray();
        Assert.Contains(ExperimentalFeatures.GameMode, keys);
        Assert.Contains(ExperimentalFeatures.StartingBonuses, keys);
        Assert.Contains(ExperimentalFeatures.ZoneContent, keys);
        Assert.Contains(ExperimentalFeatures.BordersRoads, keys);
        Assert.Contains(ExperimentalFeatures.PerTierOverrides, keys);
    }

    [Fact]
    public void ExperimentalFlags_GetSet_RoundTrip()
    {
        var flags = new ExperimentalFlags();
        foreach (var feature in ExperimentalFeatures.All)
        {
            flags.Set(feature.Key, true);
            Assert.True(flags.Get(feature.Key));
            flags.Set(feature.Key, false);
            Assert.False(flags.Get(feature.Key));
        }
    }

    [Fact]
    public void MasterFlag_DerivedFromAny_OnSave()
    {
        // No per-feature flags set ⇒ no need to advertise experimental mode.
        var file = SettingsMapper.ToFile(new GeneratorSettings(),
            advancedMode: false, experimentalMapSizes: false);
        Assert.False(file.ExperimentalEnabled);

        // One per-feature on ⇒ master derived to true so older readers still
        // light up the experimental nav.
        var flags = new ExperimentalFlags { ZoneContent = true };
        var file2 = SettingsMapper.ToFile(new GeneratorSettings(),
            advancedMode: false, experimentalMapSizes: false, experimental: flags);
        Assert.True(file2.ExperimentalEnabled);
    }
}
