using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-605: <see cref="ContentListCatalog"/> regression net. Asserts every
/// <c>includeLists</c> ID referenced by any shipped example template is
/// reachable via the catalog so the picker can surface it to the user.
/// </summary>
public class ContentListCatalogTests
{
    [Fact]
    public void All_entries_have_non_empty_id_display_and_category()
    {
        var entries = ContentListCatalog.All();
        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Id));
            Assert.False(string.IsNullOrWhiteSpace(e.Display));
            Assert.False(string.IsNullOrWhiteSpace(e.Category));
        });
    }

    [Fact]
    public void Ids_are_unique()
    {
        var entries = ContentListCatalog.All();
        var dups = entries.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dups);
    }

    [Fact]
    public void Every_category_is_in_OrderedCategories()
    {
        var ordered = new HashSet<string>(ContentListCatalog.OrderedCategories);
        var unknown = ContentListCatalog.All()
            .Select(e => e.Category)
            .Where(c => !ordered.Contains(c))
            .Distinct()
            .ToList();
        Assert.Empty(unknown);
    }

    [Fact]
    public void Grouped_returns_categories_in_OrderedCategories_order()
    {
        var keys = ContentListCatalog.Grouped().Select(g => g.Key).ToList();
        var expected = ContentListCatalog.OrderedCategories
            .Where(c => keys.Contains(c))
            .ToList();
        Assert.Equal(expected, keys);
    }

    /// <summary>
    /// Acceptance test (T-605): every <c>includeLists</c> ID used by any
    /// shipped <c>*.rmg.json</c> template must appear in the catalog so the
    /// picker can surface it. New shipped templates referencing unknown IDs
    /// will trip this assertion until the catalog is extended.
    /// </summary>
    [Fact]
    public void Catalog_covers_every_includeLists_id_in_example_templates()
    {
        var dir = ExampleTemplatesDir();
        Assert.True(Directory.Exists(dir), $"ExampleTemplates directory not found: {dir}");

        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(dir, "*.rmg.json", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            HarvestIncludeLists(doc.RootElement, seen);
        }

        var catalog = new HashSet<string>(
            ContentListCatalog.All().Select(e => e.Id),
            System.StringComparer.Ordinal);

        var missing = seen.Where(s => !catalog.Contains(s)).OrderBy(s => s).ToList();
        Assert.True(missing.Count == 0,
            "Catalog is missing includeLists IDs used by example templates: " + string.Join(", ", missing));
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

    private static void HarvestIncludeLists(JsonElement element, HashSet<string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("includeLists") && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in prop.Value.EnumerateArray())
                        {
                            if (v.ValueKind == JsonValueKind.String)
                            {
                                var s = v.GetString();
                                if (!string.IsNullOrEmpty(s)) sink.Add(s);
                            }
                        }
                    }
                    HarvestIncludeLists(prop.Value, sink);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    HarvestIncludeLists(item, sink);
                break;
        }
    }
}
