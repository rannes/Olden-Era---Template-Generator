using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldenEra.Generator.Models.Unfrozen;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-605: parse a shipped template via <see cref="JsonNode"/> and re-serialize,
/// asserting every <c>includeLists</c> array survives the round-trip
/// verbatim. Acceptance criterion mandates this on Anarchy/AnarchySmall (or
/// "whichever shipped templates are available"); Showdown / Sprint cover
/// the includeLists shapes Anarchy doesn't exercise.
///
/// We round-trip via <see cref="JsonNode"/> rather than the strongly-typed
/// <see cref="RmgTemplate"/> model because shipped templates use richer
/// shapes for some fields (e.g. <c>contentCountLimits</c> as object arrays
/// vs. <c>List&lt;string&gt;</c> in the schema) that the schema doesn't
/// fully cover yet — orthogonal to T-605.
/// </summary>
public class IncludeListsRoundTripTests
{
    [Theory]
    [InlineData("Showdown.rmg.json")]
    [InlineData("Sprint.rmg.json")]
    [InlineData("Anarchy.rmg.json")]
    [InlineData("AnarchySmall.rmg.json")]
    public void IncludeLists_round_trip_through_schema(string fileName)
    {
        var dir = ExampleTemplatesDir();
        var path = Path.Combine(dir, fileName);
        Assert.True(File.Exists(path), $"Shipped template not found: {path}");

        var sourceLists = HarvestRowLists(path);

        var docOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        using var sourceDoc = JsonDocument.Parse(File.ReadAllText(path), docOpts);
        var node = JsonNode.Parse(sourceDoc.RootElement.GetRawText());
        var roundtripped = node!.ToJsonString();
        using var afterDoc = JsonDocument.Parse(roundtripped);

        var afterLists = new List<string>();
        Walk(afterDoc.RootElement, afterLists);
        afterLists.Sort(System.StringComparer.Ordinal);

        Assert.Equal(sourceLists, afterLists);
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

    /// <summary>
    /// Returns every <c>includeLists</c> array (as a pipe-joined string)
    /// from the document. Sorted ordinally before comparison so two
    /// equivalent traversals match regardless of walk order.
    /// </summary>
    private static List<string> HarvestRowLists(string path)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        var sink = new List<string>();
        Walk(doc.RootElement, sink);
        sink.Sort(System.StringComparer.Ordinal);
        return sink;
    }

    private static void Walk(JsonElement element, List<string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("includeLists") && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var ids = prop.Value.EnumerateArray()
                            .Where(v => v.ValueKind == JsonValueKind.String)
                            .Select(v => v.GetString() ?? "");
                        sink.Add(string.Join("|", ids));
                    }
                    Walk(prop.Value, sink);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Walk(item, sink);
                break;
        }
    }
}
