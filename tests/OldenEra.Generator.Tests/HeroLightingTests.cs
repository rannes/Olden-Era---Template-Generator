using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-506 — User-controlled <c>winConditions.heroLighting</c> + <c>heroLightingDay</c>.
/// Defaults (HeroLighting=true, HeroLightingDay=1) reproduce the byte-identical
/// shipped-preset emission. Setting HeroLighting=false omits both fields.
/// Custom days round-trip through SettingsFile + ShareCodec.
/// </summary>
public class HeroLightingTests
{
    private static readonly JsonSerializerOptions EmitJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static GeneratorSettings BaseSettings() => new()
    {
        TemplateName = "T-506 Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Random,
        Seed = 42,
    };

    private static WinConditions? FirstWinConditions(RmgTemplate template) =>
        template.GameRules?.WinConditions;

    [Fact]
    public void Default_EmitsHeroLightingTrueAndDayOne()
    {
        var template = TemplateGenerator.Generate(BaseSettings());

        var wc = FirstWinConditions(template);
        Assert.NotNull(wc);
        Assert.True(wc!.HeroLighting);
        Assert.Equal(1, wc.HeroLightingDay);
    }

    [Fact]
    public void HeroLightingOff_OmitsBothFields()
    {
        var settings = BaseSettings();
        settings.GameEndConditions.HeroLighting = false;
        var template = TemplateGenerator.Generate(settings);

        var wc = FirstWinConditions(template);
        Assert.NotNull(wc);
        Assert.Null(wc!.HeroLighting);
        Assert.Null(wc.HeroLightingDay);

        // JSON emission must literally not contain either key.
        string json = JsonSerializer.Serialize(template, EmitJsonOptions);
        Assert.DoesNotContain("\"heroLighting\"", json);
        Assert.DoesNotContain("\"heroLightingDay\"", json);
    }

    [Fact]
    public void CustomDay_EmitsClampedValue()
    {
        var settings = BaseSettings();
        settings.GameEndConditions.HeroLightingDay = 7;
        var template = TemplateGenerator.Generate(settings);

        var wc = FirstWinConditions(template);
        Assert.True(wc!.HeroLighting);
        Assert.Equal(7, wc.HeroLightingDay);
    }

    [Fact]
    public void CustomDay_OutOfRange_IsClamped()
    {
        var settings = BaseSettings();
        settings.GameEndConditions.HeroLightingDay = 999;
        var template = TemplateGenerator.Generate(settings);

        Assert.Equal(30, FirstWinConditions(template)!.HeroLightingDay);

        settings.GameEndConditions.HeroLightingDay = -5;
        template = TemplateGenerator.Generate(settings);
        Assert.Equal(1, FirstWinConditions(template)!.HeroLightingDay);
    }

    [Fact]
    public void DefaultEmission_ByteIdentical_AcrossAllPresets()
    {
        // Primary acceptance gate: a fresh GeneratorSettings with default
        // HeroLighting must emit identical JSON to one that explicitly sets
        // (true, 1). Run the entire shipped preset catalog.
        var catalog = new PresetCatalog();
        Assert.NotEmpty(catalog.Entries);

        foreach (var entry in catalog.Entries)
        {
            var file = catalog.Load(entry.Id);
            var (s1, _, _, _) = SettingsMapper.FromFile(file);
            var (s2, _, _, _) = SettingsMapper.FromFile(file);
            s1.Seed = 42;
            s2.Seed = 42;

            // s1 leaves at constructor default; s2 explicitly sets.
            s2.GameEndConditions.HeroLighting = true;
            s2.GameEndConditions.HeroLightingDay = 1;

            string j1 = JsonSerializer.Serialize(TemplateGenerator.Generate(s1), EmitJsonOptions);
            string j2 = JsonSerializer.Serialize(TemplateGenerator.Generate(s2), EmitJsonOptions);

            Assert.Equal(j1, j2);
            // And every preset's emission still carries the shipped pair.
            Assert.Contains("\"heroLighting\":true", j1);
            Assert.Contains("\"heroLightingDay\":1", j1);
        }
    }

    [Fact]
    public void SettingsFile_RoundTripsHeroLighting_DefaultsOn()
    {
        var g = BaseSettings();

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        Assert.True(file.HeroLighting);
        Assert.Equal(1, file.HeroLightingDay);

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.True(back.GameEndConditions.HeroLighting);
        Assert.Equal(1, back.GameEndConditions.HeroLightingDay);
    }

    [Fact]
    public void SettingsFile_RoundTripsHeroLighting_OffWithCustomDay()
    {
        var g = BaseSettings();
        g.GameEndConditions.HeroLighting = false;
        g.GameEndConditions.HeroLightingDay = 12;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        Assert.False(file.HeroLighting);
        Assert.Equal(12, file.HeroLightingDay);

        var (back, _, _, _) = SettingsMapper.FromFile(file);
        Assert.False(back.GameEndConditions.HeroLighting);
        Assert.Equal(12, back.GameEndConditions.HeroLightingDay);
    }

    [Fact]
    public void ShareCodec_RoundTripsHeroLighting()
    {
        var g = BaseSettings();
        g.GameEndConditions.HeroLighting = false;
        g.GameEndConditions.HeroLightingDay = 9;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        var (back, _, _, _) = SettingsMapper.FromFile(decoded!);

        Assert.False(back.GameEndConditions.HeroLighting);
        Assert.Equal(9, back.GameEndConditions.HeroLightingDay);
    }
}
