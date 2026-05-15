using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-508 — per-template overrides for the per-zone random-hire creature growth
/// arrays (<c>randomHireEnableWeeklyUnitIncrement</c>,
/// <c>randomHireInitialUnitIncrement</c>). Both ship as 7-entry arrays per
/// difficulty in shipped templates (Maze, Massacre, Arcade, Junction, Universe,
/// Infinity, All Around). Default = empty list, omitted from emitted JSON
/// (byte-identical to current output). When non-empty, the override stamps
/// verbatim onto every emitted zone.
/// </summary>
public class ZoneRandomHireOverridesTests
{
    private static readonly JsonSerializerOptions EmitJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string ExampleTemplatesDir = Path.Combine(
        RepoPaths.GeneratorDataRoot(), "..", "ExampleTemplates");

    private static GeneratorSettings BaseSettings(MapTopology topology = MapTopology.Chain) => new()
    {
        TemplateName = "Random-Hire Override Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = topology,
    };

    private static IEnumerable<Zone> AllZones(RmgTemplate t) =>
        t.Variants?.SelectMany(v => v.Zones ?? Enumerable.Empty<Zone>())
        ?? Enumerable.Empty<Zone>();

    [Fact]
    public void Arcade_RandomHireFields_ParseAndRoundTrip()
    {
        // Arcade is the smallest shipped fixture that uses the random-hire
        // arrays, so it makes a tight round-trip target. (Maze / Massacre /
        // Junction / Universe / Infinity / All Around all use the same shape.)
        string path = Path.Combine(ExampleTemplatesDir, "Arcade.rmg.json");
        Assert.True(File.Exists(path), $"Fixture missing: {path}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        RmgTemplate template;
        using (var stream = File.OpenRead(path))
        {
            template = JsonSerializer.Deserialize<RmgTemplate>(stream, options)
                       ?? throw new InvalidOperationException("Failed to load Arcade");
        }

        var zones = AllZones(template).ToList();
        Assert.NotEmpty(zones);

        // Every zone in Arcade ships both arrays (7 entries each).
        var withWeekly = zones.Where(z => z.RandomHireEnableWeeklyUnitIncrement is { Count: > 0 }).ToList();
        var withInitial = zones.Where(z => z.RandomHireInitialUnitIncrement is { Count: > 0 }).ToList();
        Assert.NotEmpty(withWeekly);
        Assert.NotEmpty(withInitial);
        Assert.All(withWeekly, z => Assert.Equal(7, z.RandomHireEnableWeeklyUnitIncrement!.Count));
        Assert.All(withInitial, z => Assert.Equal(7, z.RandomHireInitialUnitIncrement!.Count));
        // Arcade values are uniformly false / 3.
        Assert.All(withWeekly, z => Assert.All(z.RandomHireEnableWeeklyUnitIncrement!, b => Assert.False(b)));
        Assert.All(withInitial, z => Assert.All(z.RandomHireInitialUnitIncrement!, i => Assert.Equal(3, i)));

        // Round-trip: serialize back, reparse — values survive.
        string json = JsonSerializer.Serialize(template, EmitJsonOptions);
        var rt = JsonSerializer.Deserialize<RmgTemplate>(json, options)!;
        var rtZones = AllZones(rt).ToList();
        Assert.Equal(zones.Count, rtZones.Count);
        for (int i = 0; i < zones.Count; i++)
        {
            Assert.Equal(zones[i].RandomHireEnableWeeklyUnitIncrement, rtZones[i].RandomHireEnableWeeklyUnitIncrement);
            Assert.Equal(zones[i].RandomHireInitialUnitIncrement, rtZones[i].RandomHireInitialUnitIncrement);
        }
    }

    [Fact]
    public void Default_RandomHireFields_NotEmittedInJson()
    {
        // Default: both lists empty → fields must be omitted from the emitted
        // JSON so existing snapshots stay byte-identical.
        var settings = BaseSettings();
        var template = TemplateGenerator.Generate(settings);
        string json = JsonSerializer.Serialize(template, EmitJsonOptions);

        Assert.DoesNotContain("randomHireEnableWeeklyUnitIncrement", json);
        Assert.DoesNotContain("randomHireInitialUnitIncrement", json);

        var zones = AllZones(template).ToList();
        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Null(z.RandomHireEnableWeeklyUnitIncrement));
        Assert.All(zones, z => Assert.Null(z.RandomHireInitialUnitIncrement));
    }

    [Fact]
    public void DefaultZoneOverrides_ProduceByteIdenticalOutput_AcrossAllPresets()
    {
        var catalog = new PresetCatalog();
        Assert.NotEmpty(catalog.Entries);

        foreach (var entry in catalog.Entries)
        {
            var file = catalog.Load(entry.Id);
            var (s1, _, _, _) = SettingsMapper.FromFile(file);
            var (s2, _, _, _) = SettingsMapper.FromFile(file);
            s1.Seed = 42;
            s2.Seed = 42;
            // Touch the new fields on s1 to ensure the empty-list sentinel is no-op.
            _ = s1.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement;
            _ = s1.ZoneOverrides.RandomHireInitialUnitIncrement;

            string j1 = JsonSerializer.Serialize(TemplateGenerator.Generate(s1), EmitJsonOptions);
            string j2 = JsonSerializer.Serialize(TemplateGenerator.Generate(s2), EmitJsonOptions);
            Assert.Equal(j2, j1);
        }
    }

    [Fact]
    public void ZoneOverrides_RandomHireEnableWeekly_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        var pattern = new List<bool> { true, true, false, false, false, false, false };
        settings.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement = pattern;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z =>
        {
            Assert.NotNull(z.RandomHireEnableWeeklyUnitIncrement);
            Assert.Equal(pattern, z.RandomHireEnableWeeklyUnitIncrement);
        });
    }

    [Fact]
    public void ZoneOverrides_RandomHireInitial_StampedOnEveryZone()
    {
        var settings = BaseSettings();
        var pattern = new List<int> { 6, 5, 5, 4, 4, 3, 3 };
        settings.ZoneOverrides.RandomHireInitialUnitIncrement = pattern;

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, z =>
        {
            Assert.NotNull(z.RandomHireInitialUnitIncrement);
            Assert.Equal(pattern, z.RandomHireInitialUnitIncrement);
        });
    }

    [Fact]
    public void ZoneOverrides_StampedListsAreNotAliased()
    {
        // Mutating one zone's list must not leak through to siblings — they
        // each get their own clone (matches the T-006 / T-503 list pattern).
        var settings = BaseSettings();
        settings.ZoneOverrides.RandomHireInitialUnitIncrement = new List<int> { 3, 3, 3, 3, 3, 3, 3 };

        var template = TemplateGenerator.Generate(settings);
        var zones = AllZones(template).Where(z => z.RandomHireInitialUnitIncrement is not null).ToList();
        Assert.True(zones.Count >= 2);

        zones[0].RandomHireInitialUnitIncrement![0] = 999;
        Assert.NotEqual(999, zones[1].RandomHireInitialUnitIncrement![0]);
    }

    [Fact]
    public void SettingsFile_RoundTripsRandomHireOverrides_ThroughMapper()
    {
        var g = BaseSettings();
        g.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement = new List<bool> { false, true, false, true, false, true, false };
        g.ZoneOverrides.RandomHireInitialUnitIncrement      = new List<int>  { 2, 3, 4, 5, 6, 7, 8 };

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        // The CSV strings must match what we emit elsewhere.
        Assert.Equal("false,true,false,true,false,true,false", file.ZoneRandomHireEnableWeeklyUnitIncrement);
        Assert.Equal("2,3,4,5,6,7,8", file.ZoneRandomHireInitialUnitIncrement);

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Equal(g.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement, back.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement);
        Assert.Equal(g.ZoneOverrides.RandomHireInitialUnitIncrement, back.ZoneOverrides.RandomHireInitialUnitIncrement);
    }

    [Fact]
    public void SettingsFile_RoundTripsRandomHireOverrides_EmptyByDefault()
    {
        var g = BaseSettings();

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        Assert.Equal("", file.ZoneRandomHireEnableWeeklyUnitIncrement);
        Assert.Equal("", file.ZoneRandomHireInitialUnitIncrement);

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Empty(back.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement);
        Assert.Empty(back.ZoneOverrides.RandomHireInitialUnitIncrement);
    }

    [Fact]
    public void SettingsFile_RoundTripsRandomHireOverrides_ThroughShareCodec()
    {
        var g = BaseSettings();
        g.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement = new List<bool> { true, false, true, false, true, false, true };
        g.ZoneOverrides.RandomHireInitialUnitIncrement      = new List<int>  { 1, 2, 3, 4, 5, 6, 7 };

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        var (back, _, _, _) = SettingsMapper.FromFile(decoded!);

        Assert.Equal(g.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement, back.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement);
        Assert.Equal(g.ZoneOverrides.RandomHireInitialUnitIncrement, back.ZoneOverrides.RandomHireInitialUnitIncrement);
    }

    [Fact]
    public void SettingsMapper_MalformedCsv_FallsBackToEmpty()
    {
        // Any malformed token discards the whole list (matches GuardReaction
        // CSV behavior). The override stays unset, the field stays omitted.
        var file = new SettingsFile
        {
            ZoneRandomHireEnableWeeklyUnitIncrement = "true,not-a-bool,false",
            ZoneRandomHireInitialUnitIncrement      = "1,2,oops",
        };

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Empty(back.ZoneOverrides.RandomHireEnableWeeklyUnitIncrement);
        Assert.Empty(back.ZoneOverrides.RandomHireInitialUnitIncrement);
    }
}
