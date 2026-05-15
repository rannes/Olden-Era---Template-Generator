using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-804: contract tests for the shared picker substring filter.
/// Locks in: empty filter passes everything, match is case-insensitive,
/// match works against any of the supplied haystack strings.
/// </summary>
public class PickerFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyFilter_AlwaysMatches(string? filter)
    {
        Assert.True(PickerFilter.Matches(filter, "anything"));
        Assert.True(PickerFilter.Matches(filter, "Boreal Call"));
    }

    [Fact]
    public void Match_IsCaseInsensitive()
    {
        Assert.True(PickerFilter.Matches("BOREAL", "Boreal Call"));
        Assert.True(PickerFilter.Matches("call", "Boreal Call"));
        Assert.True(PickerFilter.Matches("eAl c", "Boreal Call"));
    }

    [Fact]
    public void Match_TrimsFilter()
    {
        Assert.True(PickerFilter.Matches("  boreal  ", "Boreal Call"));
    }

    [Fact]
    public void NoMatch_ReturnsFalse()
    {
        Assert.False(PickerFilter.Matches("phoenix", "Boreal Call"));
    }

    [Fact]
    public void Match_AcceptsAnyHaystack()
    {
        // Picker rows often expose both id + display name + tooltip.
        Assert.True(PickerFilter.Matches("knight", "hero_alaric", "Alaric the Knight", null));
        Assert.True(PickerFilter.Matches("hero_alaric", "hero_alaric", "Alaric the Knight"));
    }

    [Fact]
    public void Match_SkipsNullHaystackEntries()
    {
        Assert.True(PickerFilter.Matches("alaric", null, "Alaric"));
        Assert.False(PickerFilter.Matches("alaric", null, null));
    }

    [Fact]
    public void Match_NoHaystacks_ReturnsFalseUnlessFilterEmpty()
    {
        Assert.False(PickerFilter.Matches("x"));
        Assert.True(PickerFilter.Matches(""));
    }
}
