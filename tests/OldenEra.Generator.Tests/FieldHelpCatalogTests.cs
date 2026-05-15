using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class FieldHelpCatalogTests
{
    [Fact]
    public void Default_loads_at_least_thirty_entries()
    {
        // T-803 acceptance: the embedded YAML must document ≥30 fields.
        Assert.True(FieldHelpCatalog.Default.Count >= 30,
            $"Expected ≥30 documented fields, got {FieldHelpCatalog.Default.Count}.");
    }

    [Fact]
    public void Default_documents_the_obscure_flags_called_out_in_T_803()
    {
        var c = FieldHelpCatalog.Default;
        Assert.NotNull(c.For(ValidationFieldKeys.MinNeutralSeparation));
        Assert.NotNull(c.For(FieldHelpKeys.GuardsRandomization));
        Assert.NotNull(c.For(FieldHelpKeys.EncounterHolesEnabled));
        Assert.NotNull(c.For(FieldHelpKeys.EncounterHolesAffectedEncounters));
        Assert.NotNull(c.For(FieldHelpKeys.EncounterHolesTwoHoleEncounters));
    }

    [Fact]
    public void For_returns_null_for_unknown_or_blank_keys()
    {
        var c = FieldHelpCatalog.Default;
        Assert.Null(c.For("does.not.exist"));
        Assert.Null(c.For(""));
        Assert.Null(c.For(null));
        Assert.Null(c.For("   "));
    }

    [Fact]
    public void Parse_reads_simple_key_value_lines()
    {
        var entries = FieldHelpCatalog.Parse("foo: bar\nbaz: qux quux\n");
        Assert.Equal("bar", entries["foo"]);
        Assert.Equal("qux quux", entries["baz"]);
    }

    [Fact]
    public void Parse_skips_comments_and_blank_lines()
    {
        var src = "# header\n\n  # indented comment\nfoo: bar\n\n";
        var entries = FieldHelpCatalog.Parse(src);
        Assert.Single(entries);
        Assert.Equal("bar", entries["foo"]);
    }

    [Fact]
    public void Parse_strips_balanced_quotes_around_value()
    {
        var entries = FieldHelpCatalog.Parse("a: \"quoted\"\nb: 'single'\nc: \"unbalanced\nd: half\"");
        Assert.Equal("quoted", entries["a"]);
        Assert.Equal("single", entries["b"]);
        // Unbalanced quotes are left as-is (the closing quote ends up missing).
        Assert.Equal("\"unbalanced", entries["c"]);
        Assert.Equal("half\"", entries["d"]);
    }

    [Fact]
    public void Parse_drops_malformed_lines_without_throwing()
    {
        var entries = FieldHelpCatalog.Parse("no colon here\n: empty key\nfoo:\nbar: ok\n");
        Assert.Single(entries);
        Assert.Equal("ok", entries["bar"]);
    }

    [Fact]
    public void Parse_keeps_colons_inside_value()
    {
        // Validator messages occasionally contain colons; only the *first*
        // colon separates key from value.
        var entries = FieldHelpCatalog.Parse("ratio: 1:2 mix\n");
        Assert.Equal("1:2 mix", entries["ratio"]);
    }

    [Fact]
    public void Custom_constructor_uses_provided_entries()
    {
        var c = new FieldHelpCatalog(new Dictionary<string, string> { ["x"] = "y" });
        Assert.Equal("y", c.For("x"));
        Assert.Null(c.For("z"));
    }

    [Fact]
    public void Validation_field_keys_are_documented()
    {
        // Every ValidationFieldKey that anchors a real validation rule should
        // also have inline help — that's the T-803 promise that the tooltip
        // surface mirrors the validator's vocabulary.
        var c = FieldHelpCatalog.Default;
        var keys = new[]
        {
            ValidationFieldKeys.TemplateName,
            ValidationFieldKeys.MapSize,
            ValidationFieldKeys.PlayerCount,
            ValidationFieldKeys.Topology,
            ValidationFieldKeys.NeutralZoneCount,
            ValidationFieldKeys.ZonesTotal,
            ValidationFieldKeys.PlayerZoneCastles,
            ValidationFieldKeys.MinNeutralSeparation,
            ValidationFieldKeys.HeroMinMax,
            ValidationFieldKeys.HeroBans,
            ValidationFieldKeys.HeroFixedStarting,
            ValidationFieldKeys.BonusPerPlayerOverrides,
            ValidationFieldKeys.ZoneContentPool,
        };
        foreach (var k in keys)
            Assert.True(c.For(k) is not null, $"Missing field-help entry for validator key '{k}'.");
    }
}
