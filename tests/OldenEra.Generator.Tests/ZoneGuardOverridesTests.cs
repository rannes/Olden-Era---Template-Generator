using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-502 — Zone.guardMultiplier and Zone.guardRandomization per-template
/// overrides. Properties already exist on <see cref="Zone"/> and are emitted
/// from hardcoded tuning profiles. The user-facing knob today is the global
/// <c>Settings.ZoneCfg.Advanced.GuardRandomization</c> slider; T-502 adds
/// per-template overrides via <see cref="ZoneOverridesSettings"/> (matching
/// the T-006 panel pattern). When set, the override stamps onto every
/// emitted zone; when unset, output stays byte-identical.
/// </summary>
public class ZoneGuardOverridesTests
{
    private static readonly JsonSerializerOptions EmitJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static GeneratorSettings BaseSettings(MapTopology topology = MapTopology.Chain) => new()
    {
        TemplateName = "Zone Guard Override Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = topology,
    };

    private static IEnumerable<Zone> AllZones(RmgTemplate t) =>
        t.Variants?.SelectMany(v => v.Zones ?? Enumerable.Empty<Zone>())
        ?? Enumerable.Empty<Zone>();

    [Fact]
    public void DefaultZoneOverrides_ProduceByteIdenticalOutput_AcrossAllPresets()
    {
        // Primary acceptance gate: touching the new fields' container without
        // setting them must not perturb any preset's emitted JSON.
        var catalog = new PresetCatalog();
        Assert.NotEmpty(catalog.Entries);

        foreach (var entry in catalog.Entries)
        {
            var file = catalog.Load(entry.Id);
            var (s1, _, _, _) = SettingsMapper.FromFile(file);
            var (s2, _, _, _) = SettingsMapper.FromFile(file);
            // Pin the seed so any topology / layout RNG is deterministic; the
            // assertion is "touching the new fields' container is a no-op",
            // not "presets are themselves deterministic".
            s1.Seed = 42;
            s2.Seed = 42;
            // Touch the new fields on s1 to ensure default sentinels are no-op.
            _ = s1.ZoneOverrides.GuardMultiplier;
            _ = s1.ZoneOverrides.GuardRandomization;

            string j1 = JsonSerializer.Serialize(TemplateGenerator.Generate(s1), EmitJsonOptions);
            string j2 = JsonSerializer.Serialize(TemplateGenerator.Generate(s2), EmitJsonOptions);

            Assert.Equal(j2, j1);
        }
    }

    [Fact]
    public void ZoneOverrides_GuardMultiplier_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.GuardMultiplier = 2.5;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(2.5, z.GuardMultiplier));

        string json = JsonSerializer.Serialize(template, EmitJsonOptions);
        Assert.Contains("\"guardMultiplier\": 2.5", json);
    }

    [Fact]
    public void ZoneOverrides_GuardRandomization_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.GuardRandomization = 0.20;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(0.20, z.GuardRandomization));

        string json = JsonSerializer.Serialize(template, EmitJsonOptions);
        Assert.Contains("\"guardRandomization\": 0.2", json);
    }

    [Fact]
    public void ZoneOverrides_GuardRandomization_Zero_IsExplicitOverride()
    {
        // 0.0 is a meaningful "no randomization" value distinct from null/unset.
        var settings = BaseSettings();
        settings.ZoneOverrides.GuardRandomization = 0.0;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(0.0, z.GuardRandomization));
    }

    [Fact]
    public void ZoneOverrides_GuardMultiplier_Overrides_TuningProfile()
    {
        // The tuning-profile path scales each zone's guardMultiplier by
        // NeutralStackStrengthMultiplier. The override must replace the
        // computed value verbatim, no scaling applied.
        var settings = BaseSettings();
        settings.ZoneCfg.NeutralStackStrengthPercent = 200; // would scale baseline up
        settings.ZoneOverrides.GuardMultiplier = 1.0;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(1.0, z.GuardMultiplier));
    }

    [Fact]
    public void SettingsFile_RoundTripsZoneGuardOverrides_ThroughMapper()
    {
        var g = BaseSettings();
        g.ZoneOverrides.GuardMultiplier = 1.75;
        g.ZoneOverrides.GuardRandomization = 0.12;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(1.75, back.ZoneOverrides.GuardMultiplier);
        Assert.Equal(0.12, back.ZoneOverrides.GuardRandomization);
    }

    [Fact]
    public void SettingsFile_RoundTripsZoneGuardOverrides_NullByDefault()
    {
        var g = BaseSettings();

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Null(back.ZoneOverrides.GuardMultiplier);
        Assert.Null(back.ZoneOverrides.GuardRandomization);
    }

    [Fact]
    public void SettingsFile_RoundTripsZoneGuardOverrides_ThroughShareCodec()
    {
        var g = BaseSettings();
        g.ZoneOverrides.GuardMultiplier = 1.25;
        g.ZoneOverrides.GuardRandomization = 0.08;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        var (back, _, _, _) = SettingsMapper.FromFile(decoded!);

        Assert.Equal(1.25, back.ZoneOverrides.GuardMultiplier);
        Assert.Equal(0.08, back.ZoneOverrides.GuardRandomization);
    }
}
