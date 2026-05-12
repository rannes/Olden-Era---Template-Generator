using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

public class ZoneContentEmitWarningsTests
{
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

    [Fact]
    public void Inspect_BiomeFilterPopulated_EmitsBiomeFilterIgnored()
    {
        var item = CleanItem("obj.biome");
        item.BiomeFilter.Add("Grass");

        var warnings = ZoneContentEmitWarnings.Inspect(item, "Zone1");

        var w = Assert.Single(warnings, x => x.Code == EmitWarning.Codes.BiomeFilterIgnored);
        Assert.Equal("Zone1", w.ZoneName);
        Assert.Equal("obj.biome", w.Sid);
    }

    [Fact]
    public void Inspect_FactionAffinityPopulated_EmitsFactionAffinityIgnored()
    {
        var item = CleanItem("obj.faction");
        item.FactionAffinity.Add("Haven");

        var warnings = ZoneContentEmitWarnings.Inspect(item, "ZoneA");

        var w = Assert.Single(warnings, x => x.Code == EmitWarning.Codes.FactionAffinityIgnored);
        Assert.Equal("ZoneA", w.ZoneName);
        Assert.Equal("obj.faction", w.Sid);
    }

    [Theory]
    [InlineData(ZoneContentPool.Guarded)]
    [InlineData(ZoneContentPool.Unguarded)]
    [InlineData(ZoneContentPool.Resources)]
    public void Inspect_PoolNotMandatory_EmitsPoolNonMandatoryDropped(ZoneContentPool pool)
    {
        var item = CleanItem("obj.pool");
        item.Pool = pool;

        var warnings = ZoneContentEmitWarnings.Inspect(item, "ZoneP");

        var w = Assert.Single(warnings, x => x.Code == EmitWarning.Codes.PoolNonMandatoryDropped);
        Assert.Equal("ZoneP", w.ZoneName);
        Assert.Equal("obj.pool", w.Sid);
    }

    [Fact]
    public void Inspect_MinCountLessThanMaxCount_EmitsMinCountRangeNarrowedToMax()
    {
        var item = CleanItem("obj.count");
        item.MinCount = 1;
        item.MaxCount = 3;

        var warnings = ZoneContentEmitWarnings.Inspect(item, "ZoneC");

        var w = Assert.Single(warnings, x => x.Code == EmitWarning.Codes.MinCountRangeNarrowedToMax);
        Assert.Equal("ZoneC", w.ZoneName);
        Assert.Equal("obj.count", w.Sid);
    }

    [Fact]
    public void Inspect_CleanItem_EmitsNoWarnings()
    {
        var item = CleanItem();

        var warnings = ZoneContentEmitWarnings.Inspect(item, "ZoneClean");

        Assert.NotNull(warnings);
        Assert.Empty(warnings);
    }
}
