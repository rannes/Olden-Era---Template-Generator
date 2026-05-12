using System;
using System.Collections.Generic;
using OldenEra.Generator.Models;
using SchemaRoad = OldenEra.Generator.Models.Unfrozen.Road;
using SchemaRoadEndpoint = OldenEra.Generator.Models.Unfrozen.RoadEndpoint;
using Zone = OldenEra.Generator.Models.Unfrozen.Zone;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneRoadDecorationEmitter
    {
        /// <summary>
        /// Appends each user-authored road decoration to <see cref="Zone.Roads"/>,
        /// initializing the list if it is null. Existing entries are preserved.
        /// </summary>
        public static void ApplyToZone(
            Zone zone,
            IReadOnlyList<ZoneRoadDecoration> decorationsForThisZone)
        {
            zone.Roads ??= new List<SchemaRoad>();
            foreach (var d in decorationsForThisZone)
            {
                zone.Roads.Add(new SchemaRoad
                {
                    Type = d.RoadType.ToString(),  // temporary; Task 2 replaces with RoadTypeToSchemaType
                    From = ToSchemaEndpoint(d.From),
                    To = ToSchemaEndpoint(d.To),
                });
            }
        }

        /// <summary>
        /// Returns the flat, deduplicated set of <see cref="ZoneRoadEndpointKind.MandatoryContent"/>
        /// arg names referenced by any endpoint of any decoration. Other endpoint kinds are ignored.
        /// </summary>
        public static IReadOnlySet<string> ReferencedItems(
            IReadOnlyList<ZoneRoadDecoration> decorations)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in decorations)
            {
                if (d.From.Kind == ZoneRoadEndpointKind.MandatoryContent) set.Add(d.From.Arg);
                if (d.To.Kind == ZoneRoadEndpointKind.MandatoryContent) set.Add(d.To.Arg);
            }
            return set;
        }

        private static SchemaRoadEndpoint ToSchemaEndpoint(ZoneRoadEndpoint e)
            => new() { Type = e.Kind.ToString(), Args = new List<string> { e.Arg } };
    }
}
