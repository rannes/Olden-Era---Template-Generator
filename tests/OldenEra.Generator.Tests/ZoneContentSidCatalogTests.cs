using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentSidCatalogTests
{
    [Fact]
    public void Catalog_contains_known_mana_well_sid()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.Contains(entries, e => e.Sid == "name_mana_well");
    }

    [Fact]
    public void Each_entry_has_friendly_name()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.FriendlyName)));
    }

    [Fact]
    public void Each_entry_has_non_empty_sid_and_category()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Sid));
            Assert.False(string.IsNullOrWhiteSpace(e.Category));
        });
    }

    [Fact]
    public void Sids_are_unique()
    {
        var entries = ZoneContentSidCatalog.All();
        var duplicates = entries
            .GroupBy(e => e.Sid)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_category_is_in_OrderedCategories()
    {
        var ordered = new HashSet<string>(ZoneContentSidCatalog.OrderedCategories);
        var unknown = ZoneContentSidCatalog.All()
            .Select(e => e.Category)
            .Where(c => !ordered.Contains(c))
            .Distinct()
            .ToList();
        Assert.Empty(unknown);
    }

    [Fact]
    public void Grouped_returns_categories_in_OrderedCategories_order()
    {
        var groupedKeys = ZoneContentSidCatalog.Grouped().Select(g => g.Key).ToList();
        var expected = ZoneContentSidCatalog.OrderedCategories
            .Where(c => groupedKeys.Contains(c))
            .ToList();
        Assert.Equal(expected, groupedKeys);
    }

    /// <summary>
    /// Regression net for catalog drift. As new shipped templates ship, this
    /// scan should fail-fast if a template references a SID the picker can't
    /// surface to the user. Mining strategy:
    /// - <c>"name": "name_*"</c>  → mandatory-content anchor SIDs (used as
    ///   <c>ZoneContentItem.Sid</c> when binding to anchored mandatory rows).
    /// - <c>"sid": "X"</c>        → object-type SIDs placed directly on the map.
    ///   Excludes <c>add_bonus_*</c> (GameRules.bonuses, not zone content).
    /// </summary>
    [Fact]
    public void Catalog_covers_every_sid_used_by_example_templates()
    {
        var templatesDir = ExampleTemplatesDir();
        Assert.True(Directory.Exists(templatesDir),
            $"ExampleTemplates directory not found: {templatesDir}");

        var sidsSeen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(templatesDir, "*.rmg.json", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            HarvestSids(doc.RootElement, sidsSeen);
        }

        var catalog = new HashSet<string>(
            ZoneContentSidCatalog.All().Select(e => e.Sid),
            System.StringComparer.Ordinal);

        var missing = sidsSeen.Where(s => !catalog.Contains(s)).OrderBy(s => s).ToList();
        Assert.True(missing.Count == 0,
            "Catalog is missing SIDs used by example templates: " + string.Join(", ", missing));
    }

    private static string ExampleTemplatesDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "src", "OldenEra.TemplateEditor", "GameData", "ExampleTemplates");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "Could not locate ExampleTemplates by walking up from AppContext.BaseDirectory.");
    }

    private static void HarvestSids(JsonElement element, HashSet<string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.Value.GetString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            // name_* mandatory-content anchors (object-level "name" fields)
                            if (prop.NameEquals("name") && s.StartsWith("name_", System.StringComparison.Ordinal))
                            {
                                sink.Add(s);
                            }
                            // raw object SIDs placed in zones / valueOverrides.
                            // Exclude GameRules-only bonus SIDs (add_bonus_*).
                            else if (prop.NameEquals("sid") && !s.StartsWith("add_bonus_", System.StringComparison.Ordinal))
                            {
                                sink.Add(s);
                            }
                        }
                    }
                    HarvestSids(prop.Value, sink);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    HarvestSids(item, sink);
                break;
        }
    }
}
