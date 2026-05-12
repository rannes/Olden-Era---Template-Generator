using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests.Settings.ZoneContent;

public class ZoneRoadTypeTests
{
    [Fact]
    public void Default_RoadType_is_Stone()
    {
        var d = new ZoneRoadDecoration();
        Assert.Equal(ZoneRoadType.Stone, d.RoadType);
    }
}
