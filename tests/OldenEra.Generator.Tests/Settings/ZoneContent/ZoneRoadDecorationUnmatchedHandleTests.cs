using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests.Settings.ZoneContent;

public class ZoneRoadDecorationUnmatchedHandleTests
{
    // Regression: a road decoration whose MandatoryContent endpoint arg matches
    // no item Handle and isn't a known auto-name should still produce a road in
    // the schema (with the literal arg preserved); it must not silently tag any
    // mandatory-content row with a Name derived from the ghost arg.
    [Fact]
    public void Decoration_with_unmatched_MandatoryContent_arg_still_emits_road_and_no_row_gains_name()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1 },
            Topology = MapTopology.Default,
            Seed = 1,
        };

        // Real zone names + connection arg observed from a generation with the
        // settings above: zones { Spawn-A, Spawn-B, Neutral-C }, connections
        // include "Ring-A-C". Do not invent strings — these are literal.
        settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "Spawn-A",
            RoadType = ZoneRoadType.Stone,
            From = new ZoneRoadEndpoint
            {
                Kind = ZoneRoadEndpointKind.MandatoryContent,
                Arg = "ghost_handle_does_not_exist",
            },
            To = new ZoneRoadEndpoint
            {
                Kind = ZoneRoadEndpointKind.Connection,
                Arg = "Ring-A-C",
            },
        });

        var template = TemplateGenerator.Generate(settings);

        // Preconditions — guard the test against silent breakage if the generator
        // renames the zone or the connection. Failure here means the literals in
        // this test need updating; failure in the regression assertions below
        // means the regression has actually returned.
        var allZones = template.Variants!.SelectMany(v => v.Zones ?? new()).ToList();
        Assert.Contains(allZones, z => z.Name == "Spawn-A");

        // 1. Road emitted on the right zone with the ghost arg preserved.
        var zone = template.Variants!
            .SelectMany(v => v.Zones ?? new())
            .Single(z => z.Name == "Spawn-A");
        Assert.NotNull(zone.Roads);
        Assert.Contains(zone.Roads!, r =>
            r.Type == "Stone" &&
            r.From!.Type == "MandatoryContent" &&
            r.From.Args![0] == "ghost_handle_does_not_exist" &&
            r.To!.Type == "Connection" &&
            r.To.Args![0] == "Ring-A-C");

        // 2. No mandatory-content row gained a Name from the ghost arg.
        var mandatory = template.MandatoryContent ?? new();
        Assert.DoesNotContain(
            mandatory.SelectMany(g => g.Content ?? new()),
            row => row.Name == "ghost_handle_does_not_exist");
    }
}
