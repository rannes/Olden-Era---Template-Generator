using OldenEra.Generator.Models;
using OldenEra.Web.Services;

namespace OldenEra.Web.Tests.Services;

public class ZoneContentCloningTests
{
    [Fact]
    public void CloneList_ReturnsIndependentItems()
    {
        var original = new ZoneContentList();
        original.Items.Add(new ZoneContentItem { Sid = "x", Handle = "h", MinCount = 2, MaxCount = 3 });

        var clone = ZoneContentCloning.CloneList(original);
        clone.Items[0].Sid = "MUTATED";
        clone.Items[0].FactionAffinity.Add("haven");

        Assert.Equal("x", original.Items[0].Sid);
        Assert.Empty(original.Items[0].FactionAffinity);
        Assert.NotSame(original.Items, clone.Items);
        Assert.NotSame(original.Items[0], clone.Items[0]);
    }

    [Fact]
    public void CloneNeutral_ProducesIndependentTierAndZoneDictionaries()
    {
        var original = new NeutralZoneContent();
        original.Global.Items.Add(new ZoneContentItem { Sid = "g" });
        original.ByTier[NeutralZoneTier.Normal] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "t" } },
        };
        original.ByZoneLetter["A"] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "z" } },
        };

        var clone = ZoneContentCloning.CloneNeutral(original);
        clone.Global.Items.Clear();
        clone.ByTier[NeutralZoneTier.Normal].Items.Clear();
        clone.ByZoneLetter["A"].Items.Clear();
        clone.ByZoneLetter["B"] = new ZoneContentList();

        Assert.Single(original.Global.Items);
        Assert.Single(original.ByTier[NeutralZoneTier.Normal].Items);
        Assert.Single(original.ByZoneLetter["A"].Items);
        Assert.False(original.ByZoneLetter.ContainsKey("B"));
    }

    [Fact]
    public void CloneRoadDecorations_IsIndependent()
    {
        var original = new List<ZoneRoadDecoration>
        {
            new() { Zone = "A", RoadType = ZoneRoadType.Stone },
        };

        var clone = ZoneContentCloning.CloneRoadDecorations(original);
        clone[0].Zone = "Z";
        clone.Add(new ZoneRoadDecoration());

        Assert.Equal("A", original[0].Zone);
        Assert.Single(original);
    }

    [Fact]
    public void CloneWithDefaultsBlanked_PreservesNonZoneContent_AndBlanksZoneTrees()
    {
        var settings = new GeneratorSettings { Seed = 42 };
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "p" });
        settings.NeutralZoneContent.Global.Items.Add(new ZoneContentItem { Sid = "n" });
        settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration { Zone = "B" });

        var clone = ZoneContentCloning.CloneWithDefaultsBlanked(settings);

        Assert.Equal(42, clone.Seed);
        Assert.Empty(clone.PlayerZoneContent.Items);
        Assert.Empty(clone.NeutralZoneContent.Global.Items);
        Assert.Empty(clone.NeutralZoneContent.ByTier);
        Assert.Empty(clone.NeutralZoneContent.ByZoneLetter);
        Assert.Empty(clone.ZoneRoadDecorations);

        // and the source is untouched:
        Assert.Single(settings.PlayerZoneContent.Items);
        Assert.Single(settings.NeutralZoneContent.Global.Items);
        Assert.Single(settings.ZoneRoadDecorations);
    }
}
