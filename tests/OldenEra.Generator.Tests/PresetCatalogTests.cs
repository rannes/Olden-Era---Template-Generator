using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class PresetCatalogTests
{
    [Fact]
    public void Entries_AreReadFromEmbeddedManifest()
    {
        var catalog = new PresetCatalog();
        // T-103 expanded the manifest to 10 archetype presets covering 2/4/6/8 player counts.
        Assert.True(catalog.Entries.Count >= 10, $"Expected ≥10 presets, got {catalog.Entries.Count}.");
        Assert.Contains(catalog.Entries, e => e.Id == "jebus-like");
        Assert.Contains(catalog.Entries, e => e.Id == "arcade-2v2");
        Assert.Contains(catalog.Entries, e => e.Id == "big-map-ffa");
        // T-103 archetypes
        Assert.Contains(catalog.Entries, e => e.Id == "blitz-rush");
        Assert.Contains(catalog.Entries, e => e.Id == "tournament-duel");
        Assert.Contains(catalog.Entries, e => e.Id == "economy-engine");
        Assert.Contains(catalog.Entries, e => e.Id == "arcane-academy");
        Assert.Contains(catalog.Entries, e => e.Id == "citadel-siege");
        Assert.Contains(catalog.Entries, e => e.Id == "six-kings");
        Assert.Contains(catalog.Entries, e => e.Id == "dragon-empire");
    }

    [Fact]
    public void EveryEntry_HasNonEmptyDescription()
    {
        var catalog = new PresetCatalog();
        foreach (var entry in catalog.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Description),
                $"Preset '{entry.Id}' is missing a description (shown in the picker UI).");
        }
    }

    [Fact]
    public void EveryPreset_GeneratesValidatorCleanTemplateOnDefaults()
    {
        // Regression net for T-103: every preset shipped in the manifest must
        // round-trip through SettingsMapper, pass SettingsValidator with no
        // blockers, and produce a non-null template via TemplateGenerator on
        // its embedded defaults. This prevents preset rot when settings or
        // validation rules evolve.
        var catalog = new PresetCatalog();
        Assert.NotEmpty(catalog.Entries);

        foreach (var entry in catalog.Entries)
        {
            var file = catalog.Load(entry.Id);
            var (settings, _, _, _) = SettingsMapper.FromFile(file);

            var result = SettingsValidator.Validate(settings);
            Assert.True(
                result.IsValid,
                $"Preset '{entry.Id}' produced validator blockers: {string.Join("; ", result.Blockers)}");

            var template = TemplateGenerator.Generate(settings);
            Assert.NotNull(template);
        }
    }

    [Fact]
    public void Load_ReturnsDeserializedSettingsFile()
    {
        var catalog = new PresetCatalog();
        var settings = catalog.Load("jebus-like");
        Assert.NotNull(settings);
        Assert.Equal("Jebus-like", settings.TemplateName);
    }

    [Fact]
    public void Load_ThrowsForUnknownId()
    {
        var catalog = new PresetCatalog();

        Assert.Throws<KeyNotFoundException>(() => catalog.Load("does-not-exist"));
    }

    [Fact]
    public void EveryManifestEntry_LoadsSuccessfully()
    {
        var catalog = new PresetCatalog();

        foreach (var entry in catalog.Entries)
        {
            var settings = catalog.Load(entry.Id);
            Assert.NotNull(settings);
        }
    }

    [Fact]
    public void EveryPreset_RoundTripsThroughJson()
    {
        var catalog = new PresetCatalog();
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        foreach (var entry in catalog.Entries)
        {
            var loaded = catalog.Load(entry.Id);
            var json = JsonSerializer.Serialize(loaded, opts);
            var roundTripped = JsonSerializer.Deserialize<SettingsFile>(json, opts);

            Assert.NotNull(roundTripped);
            Assert.Equal(loaded.TemplateName, roundTripped!.TemplateName);
            Assert.Equal(loaded.Seed, roundTripped.Seed);
        }
    }
}
