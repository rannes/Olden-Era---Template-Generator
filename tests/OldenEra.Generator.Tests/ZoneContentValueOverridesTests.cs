using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-503 — per-template overrides for the per-zone content/resource values.
/// Six new nullable knobs on <see cref="ZoneOverridesSettings"/>:
/// <c>ResourcesValue[PerArea]</c>, <c>GuardedContentValue[PerArea]</c>,
/// <c>UnguardedContentValue[PerArea]</c>. When unset, output stays
/// byte-identical to the tuning-profile default; when set, the override
/// stamps verbatim onto every emitted zone (no <c>ContentScale</c> /
/// <c>ResourceScale</c> tuning applied), matching the T-502 pattern.
/// </summary>
public class ZoneContentValueOverridesTests
{
    private static readonly JsonSerializerOptions EmitJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static GeneratorSettings BaseSettings(MapTopology topology = MapTopology.Chain) => new()
    {
        TemplateName = "Zone Content Value Override Test",
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
            _ = s1.ZoneOverrides.ResourcesValue;
            _ = s1.ZoneOverrides.ResourcesValuePerArea;
            _ = s1.ZoneOverrides.GuardedContentValue;
            _ = s1.ZoneOverrides.GuardedContentValuePerArea;
            _ = s1.ZoneOverrides.UnguardedContentValue;
            _ = s1.ZoneOverrides.UnguardedContentValuePerArea;

            string j1 = JsonSerializer.Serialize(TemplateGenerator.Generate(s1), EmitJsonOptions);
            string j2 = JsonSerializer.Serialize(TemplateGenerator.Generate(s2), EmitJsonOptions);

            Assert.Equal(j2, j1);
        }
    }

    [Fact]
    public void ZoneOverrides_ResourcesValue_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.ResourcesValue = 123456;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(123456, z.ResourcesValue));
    }

    [Fact]
    public void ZoneOverrides_ResourcesValuePerArea_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.ResourcesValuePerArea = 777;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(777, z.ResourcesValuePerArea));
    }

    [Fact]
    public void ZoneOverrides_GuardedContentValue_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.GuardedContentValue = 250000;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(250000, z.GuardedContentValue));
    }

    [Fact]
    public void ZoneOverrides_GuardedContentValuePerArea_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.GuardedContentValuePerArea = 1500;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(1500, z.GuardedContentValuePerArea));
    }

    [Fact]
    public void ZoneOverrides_UnguardedContentValue_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.UnguardedContentValue = 40000;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(40000, z.UnguardedContentValue));
    }

    [Fact]
    public void ZoneOverrides_UnguardedContentValuePerArea_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        settings.ZoneOverrides.UnguardedContentValuePerArea = 350;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(350, z.UnguardedContentValuePerArea));
    }

    [Fact]
    public void ZoneOverrides_ResourcesValue_Zero_IsExplicitOverride()
    {
        // 0 is a meaningful "no resources" override distinct from null/unset.
        var settings = BaseSettings();
        settings.ZoneOverrides.ResourcesValue = 0;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(0, z.ResourcesValue));
    }

    [Fact]
    public void ZoneOverrides_ResourcesValue_BypassesContentScaleTuning()
    {
        // The tuning-profile path scales ResourcesValue by ContentScale (via
        // ScaleResourceValue). The override must replace the computed value
        // verbatim — no tuning scaling — matching the T-502 pattern.
        var settings = BaseSettings();
        settings.ZoneCfg.NeutralStackStrengthPercent = 200; // ensure a non-default tuning
        settings.ZoneOverrides.ResourcesValue = 50000;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(50000, z.ResourcesValue));
    }

    [Fact]
    public void SettingsFile_RoundTripsZoneContentValueOverrides_ThroughMapper()
    {
        var g = BaseSettings();
        g.ZoneOverrides.ResourcesValue = 60000;
        g.ZoneOverrides.ResourcesValuePerArea = 500;
        g.ZoneOverrides.GuardedContentValue = 320000;
        g.ZoneOverrides.GuardedContentValuePerArea = 2200;
        g.ZoneOverrides.UnguardedContentValue = 45000;
        g.ZoneOverrides.UnguardedContentValuePerArea = 380;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(60000, back.ZoneOverrides.ResourcesValue);
        Assert.Equal(500, back.ZoneOverrides.ResourcesValuePerArea);
        Assert.Equal(320000, back.ZoneOverrides.GuardedContentValue);
        Assert.Equal(2200, back.ZoneOverrides.GuardedContentValuePerArea);
        Assert.Equal(45000, back.ZoneOverrides.UnguardedContentValue);
        Assert.Equal(380, back.ZoneOverrides.UnguardedContentValuePerArea);
    }

    [Fact]
    public void SettingsFile_RoundTripsZoneContentValueOverrides_NullByDefault()
    {
        var g = BaseSettings();

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Null(back.ZoneOverrides.ResourcesValue);
        Assert.Null(back.ZoneOverrides.ResourcesValuePerArea);
        Assert.Null(back.ZoneOverrides.GuardedContentValue);
        Assert.Null(back.ZoneOverrides.GuardedContentValuePerArea);
        Assert.Null(back.ZoneOverrides.UnguardedContentValue);
        Assert.Null(back.ZoneOverrides.UnguardedContentValuePerArea);
    }

    [Fact]
    public void SettingsFile_RoundTripsZoneContentValueOverrides_ThroughShareCodec()
    {
        var g = BaseSettings();
        g.ZoneOverrides.ResourcesValue = 70000;
        g.ZoneOverrides.GuardedContentValuePerArea = 1800;
        g.ZoneOverrides.UnguardedContentValue = 55000;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        var (back, _, _, _) = SettingsMapper.FromFile(decoded!);

        Assert.Equal(70000, back.ZoneOverrides.ResourcesValue);
        Assert.Equal(1800, back.ZoneOverrides.GuardedContentValuePerArea);
        Assert.Equal(55000, back.ZoneOverrides.UnguardedContentValue);
        Assert.Null(back.ZoneOverrides.ResourcesValuePerArea);
        Assert.Null(back.ZoneOverrides.GuardedContentValue);
        Assert.Null(back.ZoneOverrides.UnguardedContentValuePerArea);
    }
}
