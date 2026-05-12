using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.Web.Services;

namespace OldenEra.Web.Tests.Services;

public class ZoneContentWarningProjectionTests
{
    [Fact]
    public void PlayerItem_WithBiomeFilter_ProducesBiomeIgnoredWarning()
    {
        var settings = new GeneratorSettings();
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "x", Handle = "h1", BiomeFilter = { "snow" },
        });

        var result = ZoneContentWarningProjection.Project(settings);

        Assert.Contains(result, w => w.Scope.Kind == ZoneContentScopeKind.Player
            && w.Handle == "h1"
            && w.Warning.Code == EmitWarning.Codes.BiomeFilterIgnored);
    }

    [Fact]
    public void NeutralByTier_WithNonMandatoryPool_ProducesPoolDroppedWarning_KeyedByTier()
    {
        var settings = new GeneratorSettings();
        settings.NeutralZoneContent.ByTier[NeutralZoneTier.Rich] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "y", Handle = "rich1", Pool = ZoneContentPool.Resources } },
        };

        var result = ZoneContentWarningProjection.Project(settings);

        Assert.Contains(result, w => w.Scope.Kind == ZoneContentScopeKind.NeutralRich
            && w.Handle == "rich1"
            && w.Warning.Code == EmitWarning.Codes.PoolNonMandatoryDropped);
    }

    [Fact]
    public void NeutralByZoneLetter_KeyedByLetter()
    {
        var settings = new GeneratorSettings();
        settings.NeutralZoneContent.ByZoneLetter["B"] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "z", Handle = "zb", FactionAffinity = { "haven" } } },
        };

        var result = ZoneContentWarningProjection.Project(settings);

        Assert.Contains(result, w => w.Scope.Kind == ZoneContentScopeKind.NeutralPerZone
            && w.Scope.ZoneLetter == "B"
            && w.Handle == "zb"
            && w.Warning.Code == EmitWarning.Codes.FactionAffinityIgnored);
    }

    [Fact]
    public void NeutralGlobal_ItemsKeyedAsNeutralGlobal()
    {
        var settings = new GeneratorSettings();
        settings.NeutralZoneContent.Global.Items.Add(new ZoneContentItem
        {
            Sid = "g", Handle = "gh", BiomeFilter = { "snow" },
        });

        var result = ZoneContentWarningProjection.Project(settings);

        Assert.Contains(result, w => w.Scope.Kind == ZoneContentScopeKind.NeutralGlobal
            && w.Handle == "gh"
            && w.Warning.Code == EmitWarning.Codes.BiomeFilterIgnored);
    }

    [Fact]
    public void EmptySurface_ProducesNoWarnings()
    {
        var result = ZoneContentWarningProjection.Project(new GeneratorSettings());
        Assert.Empty(result);
    }
}
