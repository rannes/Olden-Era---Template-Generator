using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;
using Zone = OldenEra.Generator.Models.Unfrozen.Zone;

namespace OldenEra.Generator.Tests.Settings.ZoneContent;

public class ZoneRoadDecorationEmitterSchemaMappingTests
{
    [Theory]
    [InlineData(ZoneRoadType.Stone, "Stone")]
    [InlineData(ZoneRoadType.Dirt,  "Dirt")]
    public void RoadType_emits_explicit_schema_string(ZoneRoadType type, string expected)
    {
        var zone = new Zone();
        var dec  = new ZoneRoadDecoration
        {
            RoadType = type,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "x" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "y" },
        };
        ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { dec });
        Assert.Equal(expected, zone.Roads!.Single().Type);
    }

    [Theory]
    [InlineData(ZoneRoadEndpointKind.Connection,       "Connection")]
    [InlineData(ZoneRoadEndpointKind.MainObject,       "MainObject")]
    [InlineData(ZoneRoadEndpointKind.MandatoryContent, "MandatoryContent")]
    public void EndpointKind_emits_explicit_schema_string(ZoneRoadEndpointKind kind, string expected)
    {
        var zone = new Zone();
        var dec  = new ZoneRoadDecoration
        {
            RoadType = ZoneRoadType.Stone,
            From = new ZoneRoadEndpoint { Kind = kind, Arg = "a" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "b" },
        };
        ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { dec });
        Assert.Equal(expected, zone.Roads!.Single().From!.Type);
    }
}
