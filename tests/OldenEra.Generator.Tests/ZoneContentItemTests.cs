using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentItemTests
{
    [Fact]
    public void Defaults_match_design_spec()
    {
        var item = new ZoneContentItem();
        Assert.Equal("", item.Sid);
        Assert.False(item.IsGroup);
        Assert.Equal(1, item.MinCount);
        Assert.Equal(1, item.MaxCount);
        Assert.Equal(ZoneContentPool.Mandatory, item.Pool);
        Assert.False(item.IsGuarded);
        Assert.False(item.NearCastle);
        Assert.Null(item.RoadDistance);
        Assert.Empty(item.FactionAffinity);
        Assert.Empty(item.BiomeFilter);
    }

    [Fact]
    public void NeutralZoneContent_defaults_are_empty_collections()
    {
        var n = new NeutralZoneContent();
        Assert.Empty(n.Global.Items);
        Assert.Empty(n.ByTier);
        Assert.Empty(n.ByZoneLetter);
    }

    [Fact]
    public void ContentConnectionRule_defaults()
    {
        var r = new ContentConnectionRule();
        Assert.Equal(ContentRuleType.Distance, r.Type);
        Assert.Equal("", r.FromRef);
        Assert.Equal("", r.ToRef);
        Assert.Null(r.RoadType);
        Assert.Null(r.MinDistance);
        Assert.Null(r.MaxDistance);
    }
}
