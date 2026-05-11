using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ContentItemTests
{
    [Fact]
    public void Defaults_match_design_spec()
    {
        var item = new ContentItem();
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
}
