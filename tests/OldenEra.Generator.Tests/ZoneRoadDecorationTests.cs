using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneRoadDecorationTests
{
    [Fact]
    public void ZoneRoadDecoration_Defaults_AreSchemaAligned()
    {
        var d = new ZoneRoadDecoration();
        Assert.Equal("", d.Zone);
        Assert.Equal(ZoneRoadType.Stone, d.RoadType);
        Assert.NotNull(d.From);
        Assert.NotNull(d.To);
        Assert.Equal(ZoneRoadEndpointKind.Connection, d.From.Kind);
        Assert.Equal("", d.From.Arg);
    }
}
