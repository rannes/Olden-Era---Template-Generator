using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-505: Shipped hold-city templates set <c>gameRules.holdCityWinCon: true</c>
/// at the top level of <c>gameRules</c> in addition to the per-MainObject flag.
/// Verifies emission, default-omit, and round-trip preservation.
/// </summary>
public class HoldCityWinConTopLevelTests
{
    private static readonly string ExampleTemplatesDir = Path.Combine(
        RepoPaths.GeneratorDataRoot(), "..", "ExampleTemplates");

    [Fact]
    public void CityHold_Preset_SetsTopLevelHoldCityWinCon()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 2,
            MapSize = 96,
            Topology = MapTopology.HubAndSpoke,
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_5",
                CityHold = true,
            },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 2 },
        };

        var template = TemplateGenerator.Generate(settings);

        Assert.True(template.GameRules?.HoldCityWinCon);
        // The per-MainObject flag must still be set somewhere.
        bool anyHoldCityMainObject = template.Variants?
            .SelectMany(v => v.Zones ?? new List<Zone>())
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Any(o => o.HoldCityWinCon == true) ?? false;
        Assert.True(anyHoldCityMainObject);
    }

    [Fact]
    public void NonCityHold_Preset_OmitsTopLevelHoldCityWinCon()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 2,
            MapSize = 96,
            // Default victory condition; no CityHold.
        };

        var template = TemplateGenerator.Generate(settings);

        Assert.Null(template.GameRules?.HoldCityWinCon);
    }

    [Fact]
    public void NonCityHold_Preset_DoesNotEmitHoldCityWinConKey()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 2,
            MapSize = 96,
        };

        var template = TemplateGenerator.Generate(settings);
        var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        // Top-level (gameRules) must not contain holdCityWinCon when not a hold-city preset.
        // Per-MainObject flags are nested inside zones/mainObjects; we only assert the
        // top-level key is absent by checking the gameRules object substring.
        int gameRulesIdx = json.IndexOf("\"gameRules\"", System.StringComparison.Ordinal);
        Assert.True(gameRulesIdx >= 0, "gameRules object should be present");
        // Find the closing brace of gameRules. Crude but adequate for this assertion:
        // we only need to ensure the literal "holdCityWinCon" does not appear in the
        // GameRules emission. Because there is no hold-city zone, no MainObject would
        // have it either, so the simpler whole-document check is sufficient.
        Assert.DoesNotContain("\"holdCityWinCon\"", json);
    }

    [Fact]
    public void Roundtrip_ShippedHoldCityTemplate_PreservesTopLevelFlag()
    {
        string path = Path.Combine(ExampleTemplatesDir, "Zookeeper.rmg.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        using var stream = File.OpenRead(path);
        var template = JsonSerializer.Deserialize<RmgTemplate>(stream, options)
            ?? throw new InvalidOperationException($"Failed to deserialize {path}");

        Assert.True(template.GameRules?.HoldCityWinCon,
            "Shipped Zookeeper template has gameRules.holdCityWinCon: true at top level");

        // Re-serialize and ensure the flag survives.
        var writeOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        string emitted = JsonSerializer.Serialize(template, writeOptions);
        Assert.Contains("\"holdCityWinCon\":true", emitted);
    }
}
