using System.Collections.Generic;
using System.Text.Json;
using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsFileZoneContentRoundTripTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    private static SettingsFile Fixture()
    {
        var f = new SettingsFile { TemplateName = "round3" };
        f.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well",
            Handle = "name_user_mana_a",
            IsGroup = false,
            MinCount = 1,
            MaxCount = 2,
            Pool = ZoneContentPool.Mandatory,
            IsGuarded = true,
            NearCastle = true,
            RoadDistance = RoadDistance.Mid,
            FactionAffinity = new() { "haven" },
            BiomeFilter = new() { "grass" },
        });
        f.NeutralZoneContent.Global.Items.Add(new ZoneContentItem
        {
            Sid = "pandora_box",
            IsGroup = true,
            MaxCount = 3,
            Pool = ZoneContentPool.Mandatory,
            RoadDistance = RoadDistance.Far,
        });
        f.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "side_a",
            RoadType = ZoneRoadType.Dirt,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "name_user_mana_a" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" },
        });
        return f;
    }

    [Fact]
    public void RoundTrips_PlayerZoneContent_NeutralZoneContent_and_ZoneRoadDecorations()
    {
        var json  = JsonSerializer.Serialize(Fixture(), Opts);
        var back  = JsonSerializer.Deserialize<SettingsFile>(json, Opts)!;

        Assert.Single(back.PlayerZoneContent.Items);
        Assert.Equal("name_user_mana_a", back.PlayerZoneContent.Items[0].Handle);
        Assert.Equal(RoadDistance.Mid, back.PlayerZoneContent.Items[0].RoadDistance);
        Assert.Equal(new[] { "haven" }, back.PlayerZoneContent.Items[0].FactionAffinity);

        Assert.Single(back.NeutralZoneContent.Global.Items);
        Assert.Equal(3, back.NeutralZoneContent.Global.Items[0].MaxCount);
        Assert.Equal(RoadDistance.Far, back.NeutralZoneContent.Global.Items[0].RoadDistance);

        Assert.Single(back.ZoneRoadDecorations);
        Assert.Equal(ZoneRoadType.Dirt, back.ZoneRoadDecorations[0].RoadType);
        Assert.Equal(ZoneRoadEndpointKind.MandatoryContent, back.ZoneRoadDecorations[0].From.Kind);
    }

    [Fact]
    public void Enums_persist_as_strings_in_the_payload()
    {
        var json = JsonSerializer.Serialize(Fixture(), Opts);
        Assert.Contains("\"roadDistance\":\"Mid\"", json);
        Assert.Contains("\"roadType\":\"Dirt\"", json);
        Assert.Contains("\"kind\":\"MandatoryContent\"", json);
    }
}
