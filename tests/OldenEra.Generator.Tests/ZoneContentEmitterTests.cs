using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

public class ZoneContentEmitterTests
{
    private static MandatoryContentGroup NewGroup() => new MandatoryContentGroup
    {
        Name = "g",
        Content = new List<ContentItem>(),
    };

    private static ZoneContentItem CleanItem(string sid = "obj.test") => new ZoneContentItem
    {
        Sid = sid,
        IsGroup = false,
        MinCount = 1,
        MaxCount = 1,
        Pool = ZoneContentPool.Mandatory,
        IsGuarded = false,
        NearCastle = false,
        RoadDistance = null,
    };

    private static IReadOnlySet<string> NoRefs() => new HashSet<string>();

    [Fact]
    public void ApplyToMandatoryGroup_SidOnly_EmitsSidRow()
    {
        var group = NewGroup();
        var item = CleanItem("obj.x");

        var result = ZoneContentEmitter.ApplyToMandatoryGroup(
            group, new[] { item }, "side_red", NoRefs());

        Assert.Empty(result.Warnings);
        var row = Assert.Single(group.Content!);
        Assert.Equal("obj.x", row.Sid);
        Assert.Null(row.Name);
        Assert.Null(row.IncludeLists);
        Assert.Null(row.IsGuarded);
        Assert.Null(row.Rules);
    }

    [Fact]
    public void ApplyToMandatoryGroup_IsGroup_EmitsIncludeListsRow()
    {
        var group = NewGroup();
        var item = CleanItem("group.elite");
        item.IsGroup = true;

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", NoRefs());

        var row = Assert.Single(group.Content!);
        Assert.Null(row.Sid);
        Assert.NotNull(row.IncludeLists);
        Assert.Equal(new[] { "group.elite" }, row.IncludeLists);
    }

    [Fact]
    public void ApplyToMandatoryGroup_MaxCount3_RepeatsRowThreeTimes()
    {
        var group = NewGroup();
        var item = CleanItem("obj.repeat");
        item.MinCount = 3;
        item.MaxCount = 3;

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", NoRefs());

        Assert.Equal(3, group.Content!.Count);
        Assert.All(group.Content!, r => Assert.Equal("obj.repeat", r.Sid));
    }

    [Fact]
    public void ApplyToMandatoryGroup_HandleSet_UsesHandleAsName()
    {
        var group = NewGroup();
        var item = CleanItem("obj.h");
        item.Handle = "x";

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", NoRefs());

        var row = Assert.Single(group.Content!);
        Assert.Equal("x", row.Name);
    }

    [Fact]
    public void ApplyToMandatoryGroup_NoHandleButReferenced_EmitsAutoName()
    {
        var group = NewGroup();
        var item = CleanItem("obj.ref");
        var refs = new HashSet<string> { "name_user_side_red_obj.ref_0" };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", refs);

        var row = Assert.Single(group.Content!);
        Assert.Equal("name_user_side_red_obj.ref_0", row.Name);
    }

    [Fact]
    public void ApplyToMandatoryGroup_NearCastleAndRoadMid_EmitsBothPlacementRules()
    {
        var group = NewGroup();
        var item = CleanItem("obj.placed");
        item.NearCastle = true;
        item.RoadDistance = RoadDistance.Mid;

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", NoRefs());

        var row = Assert.Single(group.Content!);
        Assert.NotNull(row.Rules);
        Assert.Equal(2, row.Rules!.Count);

        var castleRule = row.Rules!.Single(r => r.Type == "MainObject");
        Assert.Equal(new[] { "0" }, castleRule.Args);
        Assert.Equal(0.05, castleRule.TargetMin);
        Assert.Equal(0.25, castleRule.TargetMax);
        Assert.Equal(1, castleRule.Weight);

        var roadRule = row.Rules!.Single(r => r.Type == "Road");
        Assert.NotNull(roadRule.Args);
        Assert.Empty(roadRule.Args!);
        Assert.Equal(0.30, roadRule.TargetMin);
        Assert.Equal(0.50, roadRule.TargetMax);
        Assert.Equal(1, roadRule.Weight);
    }

    [Fact]
    public void ApplyToMandatoryGroup_MaxCountGreaterThanOne_ProducesIndependentRulesLists()
    {
        var group = new MandatoryContentGroup { Name = "side_red", Content = new() };
        var item = new ZoneContentItem
        {
            Sid = "mana_well",
            Pool = ZoneContentPool.Mandatory,
            MaxCount = 2,
            NearCastle = true,
        };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", new HashSet<string>());

        Assert.Equal(2, group.Content!.Count);
        Assert.NotSame(group.Content[0].Rules, group.Content[1].Rules);
        Assert.NotSame(group.Content[0].Rules![0], group.Content[1].Rules![0]);
    }

    [Fact]
    public void ApplyToMandatoryGroup_PoolGuarded_SkipsRowAndWarns()
    {
        var group = NewGroup();
        var item = CleanItem("obj.guard");
        item.Pool = ZoneContentPool.Guarded;

        var result = ZoneContentEmitter.ApplyToMandatoryGroup(
            group, new[] { item }, "side_red", NoRefs());

        Assert.Empty(group.Content!);
        Assert.Contains(result.Warnings, w => w.Code == EmitWarning.Codes.PoolNonMandatoryDropped);
    }

    [Fact]
    public void ApplyToMandatoryGroup_MaxCountGreaterThanOne_AssignsPerRowOccurrenceNames()
    {
        var group = new MandatoryContentGroup { Name = "side_red", Content = new() };
        var item = new ZoneContentItem
        {
            Sid = "mana_well",
            Pool = ZoneContentPool.Mandatory,
            MaxCount = 3,
        };
        var referenced = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "name_user_side_red_mana_well_0",
            "name_user_side_red_mana_well_1",
            "name_user_side_red_mana_well_2",
        };

        ZoneContentEmitter.ApplyToMandatoryGroup(group, new[] { item }, "side_red", referenced);

        Assert.Equal(3, group.Content!.Count);
        Assert.Equal("name_user_side_red_mana_well_0", group.Content[0].Name);
        Assert.Equal("name_user_side_red_mana_well_1", group.Content[1].Name);
        Assert.Equal("name_user_side_red_mana_well_2", group.Content[2].Name);
    }
}
