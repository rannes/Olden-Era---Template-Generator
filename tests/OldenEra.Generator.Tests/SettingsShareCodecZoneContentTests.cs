using System.Collections.Generic;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsShareCodecZoneContentTests
{
    private static SettingsFile Fixture()
    {
        var f = new SettingsFile { TemplateName = "round3-share" };
        f.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well", Handle = "h", IsGroup = false,
            MinCount = 1, MaxCount = 2,
            Pool = ZoneContentPool.Mandatory,
            IsGuarded = true, NearCastle = true,
            RoadDistance = RoadDistance.Mid,
            FactionAffinity = new() { "haven" }, BiomeFilter = new() { "grass" },
        });
        f.NeutralZoneContent.Global.Items.Add(new ZoneContentItem
        {
            Sid = "pandora_box", IsGroup = true,
            MinCount = 1, MaxCount = 3,
            Pool = ZoneContentPool.Mandatory,
            RoadDistance = RoadDistance.Far,
            FactionAffinity = new() { "necro" }, BiomeFilter = new() { "snow" },
        });
        f.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "side_a",
            RoadType = ZoneRoadType.Dirt,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "h" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" },
        });
        return f;
    }

    [Fact]
    public void Encode_then_decode_preserves_zone_content_surfaces()
    {
        var original = Fixture();
        var encoded  = SettingsShareCodec.Encode(original);
        Assert.True(encoded.Length < SettingsShareCodec.MaxEncodedLength);

        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);

        Assert.Single(decoded!.PlayerZoneContent.Items);
        Assert.Equal("h", decoded.PlayerZoneContent.Items[0].Handle);
        Assert.Equal(RoadDistance.Mid, decoded.PlayerZoneContent.Items[0].RoadDistance);

        Assert.Single(decoded.NeutralZoneContent.Global.Items);
        Assert.Equal(3, decoded.NeutralZoneContent.Global.Items[0].MaxCount);

        Assert.Single(decoded.ZoneRoadDecorations);
        Assert.Equal(ZoneRoadType.Dirt, decoded.ZoneRoadDecorations[0].RoadType);
        Assert.Equal(ZoneRoadEndpointKind.MandatoryContent, decoded.ZoneRoadDecorations[0].From.Kind);
    }

    [Fact]
    public void Empty_zone_content_encodes_decodes_clean()
    {
        var f = new SettingsFile { TemplateName = "empty" };
        var encoded = SettingsShareCodec.Encode(f);
        var back    = SettingsShareCodec.TryDecode(encoded, out _);
        Assert.NotNull(back);
        Assert.Empty(back!.PlayerZoneContent.Items);
        Assert.Empty(back.NeutralZoneContent.Global.Items);
        Assert.Empty(back.ZoneRoadDecorations);
    }
}
