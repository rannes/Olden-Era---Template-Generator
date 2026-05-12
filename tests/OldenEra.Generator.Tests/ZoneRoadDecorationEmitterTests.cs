using System.Collections.Generic;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;
using SchemaRoad = OldenEra.Generator.Models.Unfrozen.Road;
using Zone = OldenEra.Generator.Models.Unfrozen.Zone;

namespace OldenEra.Generator.Tests
{
    public class ZoneRoadDecorationEmitterTests
    {
        [Theory]
        [InlineData(ZoneRoadEndpointKind.Connection, "Connection")]
        [InlineData(ZoneRoadEndpointKind.MainObject, "MainObject")]
        [InlineData(ZoneRoadEndpointKind.MandatoryContent, "MandatoryContent")]
        public void ApplyToZone_EmitsEndpointKindAndArg(ZoneRoadEndpointKind kind, string expectedSchemaType)
        {
            var zone = new Zone();
            var deco = new ZoneRoadDecoration
            {
                Zone = "Z1",
                RoadType = "Stone",
                From = new ZoneRoadEndpoint { Kind = kind, Arg = "argA" },
                To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "argB" },
            };

            ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { deco });

            Assert.NotNull(zone.Roads);
            var road = Assert.Single(zone.Roads!);
            Assert.NotNull(road.From);
            Assert.Equal(expectedSchemaType, road.From!.Type);
            Assert.NotNull(road.From.Args);
            Assert.Equal("argA", road.From.Args![0]);
        }

        [Theory]
        [InlineData("Stone")]
        [InlineData("Dirt")]
        public void ApplyToZone_HonoursRoadType(string roadType)
        {
            var zone = new Zone();
            var deco = new ZoneRoadDecoration
            {
                Zone = "Z1",
                RoadType = roadType,
                From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "a" },
                To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "b" },
            };

            ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { deco });

            Assert.Equal(roadType, zone.Roads![0].Type);
        }

        [Fact]
        public void ApplyToZone_InitializesNullRoadsList()
        {
            var zone = new Zone { Roads = null };
            var deco = new ZoneRoadDecoration
            {
                From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "a" },
                To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "b" },
            };

            ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { deco });

            Assert.NotNull(zone.Roads);
            Assert.Single(zone.Roads!);
        }

        [Fact]
        public void ApplyToZone_AppendsAndDoesNotReplace()
        {
            var existing = new SchemaRoad { Type = "Stone" };
            var zone = new Zone { Roads = new List<SchemaRoad> { existing } };
            var deco = new ZoneRoadDecoration
            {
                RoadType = "Dirt",
                From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "a" },
                To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "b" },
            };

            ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { deco });

            Assert.Equal(2, zone.Roads!.Count);
            Assert.Same(existing, zone.Roads[0]);
            Assert.Equal("Dirt", zone.Roads[1].Type);
        }

        [Fact]
        public void ReferencedItems_ReturnsOnlyMandatoryContentArgs()
        {
            var decos = new[]
            {
                new ZoneRoadDecoration
                {
                    From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "item.A" },
                    To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "conn.X" },
                },
                new ZoneRoadDecoration
                {
                    From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MainObject, Arg = "main.Y" },
                    To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "item.B" },
                },
            };

            var refs = ZoneRoadDecorationEmitter.ReferencedItems(decos);

            Assert.Equal(2, refs.Count);
            Assert.Contains("item.A", refs);
            Assert.Contains("item.B", refs);
            Assert.DoesNotContain("conn.X", refs);
            Assert.DoesNotContain("main.Y", refs);
        }

        [Fact]
        public void ReferencedItems_Deduplicates()
        {
            var decos = new[]
            {
                new ZoneRoadDecoration
                {
                    From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "item.A" },
                    To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "conn" },
                },
                new ZoneRoadDecoration
                {
                    From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "item.A" },
                    To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "item.A" },
                },
            };

            var refs = ZoneRoadDecorationEmitter.ReferencedItems(decos);

            Assert.Single(refs);
            Assert.Contains("item.A", refs);
        }
    }
}
