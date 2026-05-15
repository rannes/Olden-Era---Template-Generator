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
/// T-509 — MainObject schema completion. Shipped templates set extra
/// per-MainObject fields (<c>owner</c>, <c>isKeyObject</c>,
/// <c>enableWeeklyUnitIncrement</c>, <c>initialUnitIncrement</c>,
/// <c>factions</c>) that the model previously dropped on load. Round-trip-only:
/// the generator never emits these on its own, but a loaded template must
/// preserve them through serialize → deserialize → serialize.
/// </summary>
public class MainObjectSchemaCompletionTests
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions EmitOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string ExampleTemplatesDir = Path.Combine(
        RepoPaths.GeneratorDataRoot(), "..", "ExampleTemplates");

    private static RmgTemplate Load(string fileName)
    {
        string path = Path.Combine(ExampleTemplatesDir, fileName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<RmgTemplate>(stream, ReadOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize {path}");
    }

    private static IEnumerable<MainObject> AllMainObjects(RmgTemplate t) =>
        (t.Variants ?? new List<Variant>())
            .SelectMany(v => v.Zones ?? new List<Zone>())
            .SelectMany(z => z.MainObjects ?? new List<MainObject>());

    [Fact]
    public void Harmony_IsKeyObjectAndOwner_RoundTrip()
    {
        // Harmony spawns carry `isKeyObject: true`; their paired city carries `owner: "PlayerN"`.
        var template = Load("Harmony.rmg.json");

        var spawnsWithKey = AllMainObjects(template)
            .Where(o => o.IsKeyObject == true)
            .ToList();
        Assert.NotEmpty(spawnsWithKey);

        var ownedObjects = AllMainObjects(template)
            .Where(o => !string.IsNullOrEmpty(o.Owner))
            .ToList();
        Assert.NotEmpty(ownedObjects);
        Assert.Contains(ownedObjects, o => o.Owner == "Player1");

        // Re-serialize and ensure both flags survive.
        string emitted = JsonSerializer.Serialize(template, EmitOptions);
        Assert.Contains("\"isKeyObject\": true", emitted);
        Assert.Contains("\"owner\": \"Player1\"", emitted);
    }

    [Fact]
    public void Shamrock_EnableWeeklyAndInitialUnitIncrement_RoundTrip()
    {
        // Shamrock's neutral cities carry both unit-increment fields (bool / int).
        var template = Load("Shamrock.rmg.json");

        var withWeekly = AllMainObjects(template)
            .Where(o => o.EnableWeeklyUnitIncrement == true)
            .ToList();
        Assert.NotEmpty(withWeekly);

        var withInitial = AllMainObjects(template)
            .Where(o => o.InitialUnitIncrement.HasValue)
            .ToList();
        Assert.NotEmpty(withInitial);
        Assert.Contains(withInitial, o => o.InitialUnitIncrement == 2);

        string emitted = JsonSerializer.Serialize(template, EmitOptions);
        Assert.Contains("\"enableWeeklyUnitIncrement\": true", emitted);
        Assert.Contains("\"initialUnitIncrement\": 2", emitted);
    }

    [Fact]
    public void Hallway_OwnerAndFactions_RoundTrip()
    {
        // Hallway: City carries `owner`, Spawn carries an explicit empty `factions` array.
        var template = Load("Hallway.rmg.json");

        Assert.Contains(AllMainObjects(template), o => o.Owner == "Player1");
        Assert.Contains(AllMainObjects(template), o => o.Owner == "Player2");

        // factions array is present-and-empty on Spawn objects in Hallway.
        var spawnsWithFactions = AllMainObjects(template)
            .Where(o => o.Type == "Spawn" && o.Factions != null)
            .ToList();
        Assert.NotEmpty(spawnsWithFactions);
        Assert.All(spawnsWithFactions, o => Assert.Empty(o.Factions!));

        string emitted = JsonSerializer.Serialize(template, EmitOptions);
        Assert.Contains("\"owner\": \"Player1\"", emitted);
        Assert.Contains("\"owner\": \"Player2\"", emitted);
        // Empty factions array must still appear (model field is non-null after load).
        Assert.Contains("\"factions\":", emitted);
    }

    [Fact]
    public void GeneratorDefaults_DoNotEmitNewFields_AcrossAllPresets()
    {
        var catalog = new PresetCatalog();
        Assert.NotEmpty(catalog.Entries);

        foreach (var entry in catalog.Entries)
        {
            var file = catalog.Load(entry.Id);
            var (settings, _, _, _) = SettingsMapper.FromFile(file);
            settings.Seed = 42;

            string json = JsonSerializer.Serialize(TemplateGenerator.Generate(settings), EmitOptions);

            // None of the new fields should ever appear in default generator output.
            Assert.DoesNotContain("\"owner\":", json);
            Assert.DoesNotContain("\"isKeyObject\"", json);
            Assert.DoesNotContain("\"enableWeeklyUnitIncrement\"", json);
            Assert.DoesNotContain("\"initialUnitIncrement\"", json);
            // `factions` (plural) on a MainObject is distinct from any other key;
            // generator output should not produce it under default settings.
            Assert.DoesNotContain("\"factions\"", json);
        }
    }

    [Fact]
    public void GeneratorDefaults_ByteIdentical_AcrossAllPresets()
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

            string j1 = JsonSerializer.Serialize(TemplateGenerator.Generate(s1), EmitOptions);
            string j2 = JsonSerializer.Serialize(TemplateGenerator.Generate(s2), EmitOptions);
            Assert.Equal(j2, j1);
        }
    }
}
