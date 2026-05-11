using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentResolverTests
{
    [Fact]
    public void Empty_NeutralZoneContent_resolves_to_empty_list()
    {
        var cfg = new NeutralZoneContent();
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Red-A");
        Assert.Empty(resolved.Items);
    }

    [Fact]
    public void Global_items_appear_in_resolved_output()
    {
        var cfg = new NeutralZoneContent();
        cfg.Global.Items.Add(new ZoneContentItem { Sid = "name_mana_well" });
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Red-A");
        Assert.Single(resolved.Items);
        Assert.Equal("name_mana_well", resolved.Items[0].Sid);
    }
}
