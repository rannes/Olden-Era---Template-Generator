using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OldenEra.Generator.Models.Unfrozen;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-605 (review follow-up): verify shipped templates' <c>includeLists</c>
/// arrays round-trip through the strongly-typed <see cref="RmgTemplate"/>
/// schema — not just through a JSON-parser passthrough. The check
/// deserializes each fixture into <see cref="RmgTemplate"/>, harvests every
/// <see cref="ContentItem.IncludeLists"/> in document order, re-serializes
/// the model, harvests again, and asserts the two sequences are equal as
/// ordered lists.
///
/// <para>
/// Coverage today: <c>Anarchy.rmg.json</c> and <c>Sprint.rmg.json</c>. The
/// remaining shipped templates (<c>Showdown.rmg.json</c>,
/// <c>AnarchySmall.rmg.json</c>, and others) cannot deserialize through the
/// current schema because <see cref="Zone.ContentCountLimits"/> is modelled
/// as <c>List&lt;string&gt;</c> while those fixtures encode it as an array
/// of objects. That's a pre-existing schema gap orthogonal to T-605; see
/// the inline-data comments below. Once the schema gap is closed those
/// fixtures should be added to this theory.
/// </para>
/// </summary>
public class IncludeListsSchemaRoundTripTests
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    [Theory]
    // Anarchy + Sprint deserialize cleanly today. Both exercise non-empty
    // ContentItem.IncludeLists arrays in MandatoryContent groups.
    [InlineData("Anarchy.rmg.json")]
    [InlineData("Sprint.rmg.json")]
    // TODO: re-enable once Zone.contentCountLimits is modelled as objects.
    // [InlineData("Showdown.rmg.json")]
    // [InlineData("AnarchySmall.rmg.json")]
    public void IncludeLists_round_trip_through_RmgTemplate_schema(string fileName)
    {
        var dir = ExampleTemplatesDir();
        var path = Path.Combine(dir, fileName);
        Assert.True(File.Exists(path), $"Shipped template not found: {path}");

        var sourceJson = File.ReadAllText(path);
        var template = JsonSerializer.Deserialize<RmgTemplate>(sourceJson, DeserializeOptions);
        Assert.NotNull(template);

        var before = HarvestIncludeLists(template!);

        // Serialize back and re-deserialize; harvest again. The before/after
        // sequences should match as ordered lists.
        var roundTrippedJson = JsonSerializer.Serialize(template);
        var roundTripped = JsonSerializer.Deserialize<RmgTemplate>(roundTrippedJson, DeserializeOptions);
        Assert.NotNull(roundTripped);

        var after = HarvestIncludeLists(roundTripped!);

        Assert.Equal(before, after); // ordered list equality, not sorted
    }

    /// <summary>
    /// Walks the deserialized <see cref="RmgTemplate"/> and returns every
    /// <see cref="ContentItem.IncludeLists"/> array (pipe-joined) in document
    /// order. <c>null</c> arrays are recorded as the literal string
    /// <c>"&lt;null&gt;"</c> so they can be distinguished from empty arrays.
    /// </summary>
    private static List<string> HarvestIncludeLists(RmgTemplate template)
    {
        var sink = new List<string>();
        var groups = template.MandatoryContent;
        if (groups is null) return sink;

        foreach (var group in groups)
        {
            if (group?.Content is null) continue;
            foreach (var item in group.Content)
            {
                if (item is null) continue;
                sink.Add(item.IncludeLists is null
                    ? "<null>"
                    : string.Join("|", item.IncludeLists));
            }
        }
        return sink;
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
        throw new DirectoryNotFoundException("ExampleTemplates directory not found from BaseDirectory.");
    }
}
