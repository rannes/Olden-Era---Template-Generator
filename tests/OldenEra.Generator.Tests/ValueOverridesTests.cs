using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-003 — RmgTemplate.valueOverrides editor support.
/// Verifies emission shape (matches shipped templates), default-omission
/// (clean diffs), validation skipping, dedup, and SettingsFile round-trip.
/// </summary>
public class ValueOverridesTests
{
    private static readonly JsonSerializerOptions EmitJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static GeneratorSettings MakeSettings() => new()
    {
        PlayerCount = 4,
        ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 2 },
        Topology = MapTopology.Default,
    };

    [Fact]
    public void ValueOverrides_Empty_FieldIsOmittedFromJson()
    {
        var s = MakeSettings();
        var template = TemplateGenerator.Generate(s);

        Assert.Null(template.ValueOverrides);

        string json = JsonSerializer.Serialize(template, EmitJsonOptions);
        Assert.DoesNotContain("\"valueOverrides\"", json);
    }

    [Fact]
    public void ValueOverrides_NonEmpty_EmitsExpectedShape()
    {
        var s = MakeSettings();
        s.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "boreal_call", Variant = -1, GuardValue = 6000 },
            new() { Sid = "jousting_range", Variant = -1, GuardValue = 6000 },
        };

        var template = TemplateGenerator.Generate(s);

        Assert.NotNull(template.ValueOverrides);
        Assert.Equal(2, template.ValueOverrides!.Count);
        Assert.Equal("boreal_call", template.ValueOverrides[0].Sid);
        Assert.Equal(-1, template.ValueOverrides[0].Variant);
        Assert.Equal(6000, template.ValueOverrides[0].GuardValue);

        string json = JsonSerializer.Serialize(template, EmitJsonOptions);
        Assert.Contains("\"valueOverrides\"", json);
        Assert.Contains("\"sid\": \"boreal_call\"", json);
        Assert.Contains("\"variant\": -1", json);
        Assert.Contains("\"guardValue\": 6000", json);
    }

    [Fact]
    public void ValueOverrides_SkipsRowsWithBlankSid()
    {
        var s = MakeSettings();
        s.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "",   Variant = -1, GuardValue = 5000 },
            new() { Sid = "  ", Variant = -1, GuardValue = 5000 },
            new() { Sid = "valid_sid", Variant = -1, GuardValue = 5000 },
        };

        var template = TemplateGenerator.Generate(s);

        Assert.NotNull(template.ValueOverrides);
        var row = Assert.Single(template.ValueOverrides!);
        Assert.Equal("valid_sid", row.Sid);
    }

    [Fact]
    public void ValueOverrides_SkipsRowsWithNonPositiveGuardValue()
    {
        var s = MakeSettings();
        s.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "x", Variant = -1, GuardValue = 0 },
            new() { Sid = "y", Variant = -1, GuardValue = -100 },
            new() { Sid = "z", Variant = -1, GuardValue = 1 },
        };

        var template = TemplateGenerator.Generate(s);

        var row = Assert.Single(template.ValueOverrides!);
        Assert.Equal("z", row.Sid);
    }

    [Fact]
    public void ValueOverrides_DedupesOnSidAndVariantPair()
    {
        var s = MakeSettings();
        s.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "a", Variant = -1, GuardValue = 100 },
            new() { Sid = "a", Variant = -1, GuardValue = 999 }, // dup, dropped
            new() { Sid = "a", Variant =  0, GuardValue = 200 }, // different variant, kept
        };

        var template = TemplateGenerator.Generate(s);

        Assert.Equal(2, template.ValueOverrides!.Count);
        Assert.Equal(100, template.ValueOverrides[0].GuardValue); // first wins
        Assert.Equal(0,   template.ValueOverrides[1].Variant);
    }

    [Fact]
    public void ValueOverrides_RoundTripsThroughSettingsFile()
    {
        var g = new GeneratorSettings();
        g.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "boreal_call", Variant = -1, GuardValue = 6000 },
            new() { Sid = "petrified_memorial", Variant = 2, GuardValue = 7500 },
        };

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var roundTripped = SettingsMapper.FromFile(file).Settings;

        Assert.Equal(2, roundTripped.Content.ValueOverrides.Count);
        Assert.Equal("boreal_call", roundTripped.Content.ValueOverrides[0].Sid);
        Assert.Equal(-1, roundTripped.Content.ValueOverrides[0].Variant);
        Assert.Equal(6000, roundTripped.Content.ValueOverrides[0].GuardValue);
        Assert.Equal("petrified_memorial", roundTripped.Content.ValueOverrides[1].Sid);
        Assert.Equal(2, roundTripped.Content.ValueOverrides[1].Variant);
        Assert.Equal(7500, roundTripped.Content.ValueOverrides[1].GuardValue);
    }

    [Fact]
    public void ValueOverrides_RoundTripsThroughSettingsFileJson()
    {
        var g = new GeneratorSettings();
        g.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "scholar", Variant = -1, GuardValue = 4500 },
        };

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        var rehydratedFile = JsonSerializer.Deserialize<SettingsFile>(json)!;
        var rehydrated = SettingsMapper.FromFile(rehydratedFile).Settings;

        var row = Assert.Single(rehydrated.Content.ValueOverrides);
        Assert.Equal("scholar", row.Sid);
        Assert.Equal(4500, row.GuardValue);
    }

    [Fact]
    public void ValueOverrides_RoundTripsThroughSettingsShareCodec()
    {
        var g = new GeneratorSettings();
        g.Content.ValueOverrides = new List<ValueOverrideSetting>
        {
            new() { Sid = "boreal_call", Variant = -1, GuardValue = 6000 },
        };
        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);

        var encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Single(decoded!.ValueOverrides);
        Assert.Equal("boreal_call", decoded.ValueOverrides[0].Sid);
        Assert.Equal(6000, decoded.ValueOverrides[0].GuardValue);
    }

    [Fact]
    public void ValueOverrides_DefaultGenerator_OutputUnchangedForOmittedField()
    {
        // Ensures the new emission block is gated on count > 0 — no field, no
        // ordering shift, no whitespace diff for users who don't touch it.
        var s = MakeSettings();
        var template = TemplateGenerator.Generate(s);

        Assert.Null(template.ValueOverrides);
    }
}
