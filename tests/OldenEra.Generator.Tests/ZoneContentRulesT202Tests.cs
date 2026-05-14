using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-202 — explicit <see cref="ContentPlacementRule"/> editor on user-authored
/// zone-content items. Covers emission shape, CSV codec round-trip, JSON
/// round-trip through <c>ZoneContentItem</c>, and malformed-input safety.
/// </summary>
public class ZoneContentRulesT202Tests
{
    private static MandatoryContentGroup NewGroup() =>
        new() { Name = "g", Content = new List<ContentItem>() };

    [Fact]
    public void Emit_EmptyRulesList_OmitsRulesField()
    {
        var group = NewGroup();
        var item = new ZoneContentItem { Sid = "obj.x" };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", new HashSet<string>());

        var row = Assert.Single(group.Content!);
        Assert.Null(row.Rules);
    }

    [Fact]
    public void Emit_UserRule_AppendsExactJsonShape()
    {
        var group = NewGroup();
        var item = new ZoneContentItem
        {
            Sid = "name_pandora_box_resources",
            Rules = new()
            {
                new ZoneContentRule { Type = "Crossroads", TargetMin = 0.15, TargetMax = 0.30, Weight = 1 },
            },
        };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "neutral_a", new HashSet<string>());

        var row = Assert.Single(group.Content!);
        var rule = Assert.Single(row.Rules!);
        Assert.Equal("Crossroads", rule.Type);
        Assert.NotNull(rule.Args);
        Assert.Empty(rule.Args!);
        Assert.Equal(0.15, rule.TargetMin);
        Assert.Equal(0.30, rule.TargetMax);
        Assert.Equal(1, rule.Weight);
    }

    [Fact]
    public void Emit_UserRulesAfterConvenienceRules_AppendInDeclaredOrder()
    {
        var group = NewGroup();
        var item = new ZoneContentItem
        {
            Sid = "obj.combo",
            NearCastle = true, // emits MainObject rule first
            Rules = new()
            {
                new ZoneContentRule { Type = "Crossroads", TargetMin = 0.10, TargetMax = 0.20, Weight = 1 },
                new ZoneContentRule { Type = "Border",     TargetMin = 0.05, TargetMax = 0.10, Weight = 1 },
            },
        };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", new HashSet<string>());

        var row = Assert.Single(group.Content!);
        Assert.NotNull(row.Rules);
        Assert.Equal(3, row.Rules!.Count);
        Assert.Equal("MainObject", row.Rules[0].Type); // from NearCastle convenience
        Assert.Equal("Crossroads", row.Rules[1].Type);
        Assert.Equal("Border",     row.Rules[2].Type);
    }

    [Fact]
    public void Emit_RuleWithEmptyType_IsSkipped()
    {
        var group = NewGroup();
        var item = new ZoneContentItem
        {
            Sid = "obj.x",
            Rules = new()
            {
                new ZoneContentRule { Type = "" }, // empty → dropped
                new ZoneContentRule { Type = "Road", TargetMin = 0.1, TargetMax = 0.2, Weight = 1 },
            },
        };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", new HashSet<string>());

        var row = Assert.Single(group.Content!);
        var rule = Assert.Single(row.Rules!);
        Assert.Equal("Road", rule.Type);
    }

    [Fact]
    public void Csv_RoundTrip_PreservesAllFields()
    {
        var rules = new List<ZoneContentRule>
        {
            new() { Type = "MainObject", Args = new() { "0" }, TargetMin = 0.05, TargetMax = 0.25, Weight = 1 },
            new() { Type = "Crossroads", TargetMin = 0.15, TargetMax = 0.30, Weight = 2 },
        };
        var csv = ZoneContentRuleCsv.Join(rules);
        var parsed = ZoneContentRuleCsv.Parse(csv);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("MainObject", parsed[0].Type);
        Assert.Equal(new[] { "0" }, parsed[0].Args);
        Assert.Equal(0.05, parsed[0].TargetMin);
        Assert.Equal(0.25, parsed[0].TargetMax);
        Assert.Equal(1, parsed[0].Weight);
        Assert.Equal("Crossroads", parsed[1].Type);
        Assert.Empty(parsed[1].Args);
        Assert.Equal(2, parsed[1].Weight);
    }

    [Fact]
    public void Csv_Parse_EmptyOrWhitespace_ReturnsEmptyList()
    {
        Assert.Empty(ZoneContentRuleCsv.Parse(null));
        Assert.Empty(ZoneContentRuleCsv.Parse(""));
        Assert.Empty(ZoneContentRuleCsv.Parse("   "));
    }

    [Fact]
    public void Csv_Parse_Malformed_DoesNotThrow_SkipsBadEntries()
    {
        // Rule 1: completely garbage numeric fields → numeric fields just go null,
        // but Type is present so the rule itself is kept.
        // Rule 2: empty type token → dropped entirely.
        // Rule 3: truncated (no min/max/weight) → kept with nulls.
        var parsed = ZoneContentRuleCsv.Parse("MainObject|0|nope|alsobad|whatever; |args|0.1|0.2|1; Road");

        Assert.Equal(2, parsed.Count);
        Assert.Equal("MainObject", parsed[0].Type);
        Assert.Null(parsed[0].TargetMin);
        Assert.Null(parsed[0].TargetMax);
        Assert.Null(parsed[0].Weight);
        Assert.Equal("Road", parsed[1].Type);
        Assert.Empty(parsed[1].Args);
    }

    [Fact]
    public void Json_RoundTrip_PreservesRulesOnZoneContentItem()
    {
        // Persistence layer #2: SettingsFile JSON serialization. Adding a new
        // List<> property would silently break round-tripping if the model
        // didn't deserialize cleanly — pin it.
        var original = new ZoneContentItem
        {
            Sid = "name_pandora_box_resources",
            Rules = new()
            {
                new ZoneContentRule
                {
                    Type = "Crossroads",
                    Args = new() { "a", "b" },
                    TargetMin = 0.15, TargetMax = 0.30, Weight = 1
                },
            },
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(original, opts);
        var back = JsonSerializer.Deserialize<ZoneContentItem>(json, opts);

        Assert.NotNull(back);
        var rule = Assert.Single(back!.Rules);
        Assert.Equal("Crossroads", rule.Type);
        Assert.Equal(new[] { "a", "b" }, rule.Args);
        Assert.Equal(0.15, rule.TargetMin);
        Assert.Equal(0.30, rule.TargetMax);
        Assert.Equal(1, rule.Weight);
    }

    [Fact]
    public void Cloning_DeepClonesRulesList()
    {
        var source = new ZoneContentItem
        {
            Sid = "x",
            Rules = new()
            {
                new ZoneContentRule { Type = "Road", Args = new() { "z" }, TargetMin = 0.1 },
            },
        };

        var clone = ZoneContentCloning.CloneItem(source);

        Assert.NotSame(source.Rules, clone.Rules);
        Assert.NotSame(source.Rules[0], clone.Rules[0]);
        Assert.NotSame(source.Rules[0].Args, clone.Rules[0].Args);
        Assert.Equal("Road", clone.Rules[0].Type);
        Assert.Equal(new[] { "z" }, clone.Rules[0].Args);
    }

    [Fact]
    public void DemoPreset_PandoraResourcesNearCrossroads_CarriesRule()
    {
        // Ships proof-of-end-to-end: the curated preset surfaces the new field.
        var preset = ZoneContentPresets.All().FirstOrDefault(p =>
            p.Name == "Pandora Resources x1 (near crossroads)");
        Assert.NotNull(preset);
        var rule = Assert.Single(preset!.Item.Rules);
        Assert.Equal("Crossroads", rule.Type);
        Assert.Equal(0.15, rule.TargetMin);
        Assert.Equal(0.30, rule.TargetMax);
    }
}
