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

    [Fact]
    public void Tier_items_append_to_global()
    {
        var cfg = new NeutralZoneContent();
        cfg.Global.Items.Add(new ZoneContentItem { Sid = "name_mana_well" });
        cfg.ByTier[NeutralZoneTier.Rich] = new ZoneContentList();
        cfg.ByTier[NeutralZoneTier.Rich].Items.Add(new ZoneContentItem { Sid = "name_pandora_box_army" });
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Rich, "Red-A");
        Assert.Equal(2, resolved.Items.Count);
        Assert.Contains(resolved.Items, i => i.Sid == "name_pandora_box_army");
    }

    [Fact]
    public void Tier_replaces_same_Sid_from_global()
    {
        var cfg = new NeutralZoneContent();
        cfg.Global.Items.Add(new ZoneContentItem { Sid = "name_mana_well", MaxCount = 1 });
        cfg.ByTier[NeutralZoneTier.Rich] = new ZoneContentList();
        cfg.ByTier[NeutralZoneTier.Rich].Items.Add(new ZoneContentItem { Sid = "name_mana_well", MaxCount = 4 });
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Rich, "Red-A");
        Assert.Single(resolved.Items);
        Assert.Equal(4, resolved.Items[0].MaxCount);
    }

    [Fact]
    public void Other_tier_does_not_apply()
    {
        var cfg = new NeutralZoneContent();
        cfg.ByTier[NeutralZoneTier.Rich] = new ZoneContentList();
        cfg.ByTier[NeutralZoneTier.Rich].Items.Add(new ZoneContentItem { Sid = "name_pandora_box_army" });
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Poor, "Red-A");
        Assert.Empty(resolved.Items);
    }

    [Fact]
    public void Letter_replaces_tier_for_that_zone()
    {
        var cfg = new NeutralZoneContent();
        cfg.ByTier[NeutralZoneTier.Normal] = new ZoneContentList();
        cfg.ByTier[NeutralZoneTier.Normal].Items.Add(new ZoneContentItem { Sid = "name_mana_well", MaxCount = 1 });
        cfg.ByZoneLetter["Red-A"] = new ZoneContentList();
        cfg.ByZoneLetter["Red-A"].Items.Add(new ZoneContentItem { Sid = "name_mana_well", MaxCount = 7 });
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Red-A");
        Assert.Single(resolved.Items);
        Assert.Equal(7, resolved.Items[0].MaxCount);
    }

    [Fact]
    public void Letter_only_applies_to_that_letter()
    {
        var cfg = new NeutralZoneContent();
        cfg.ByZoneLetter["Red-A"] = new ZoneContentList();
        cfg.ByZoneLetter["Red-A"].Items.Add(new ZoneContentItem { Sid = "name_mana_well" });
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Orange-A");
        Assert.Empty(resolved.Items);
    }
}
