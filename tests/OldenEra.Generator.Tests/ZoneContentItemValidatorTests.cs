using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentItemValidatorTests
{
    [Fact]
    public void Default_item_reports_empty_sid()
    {
        var item = new ZoneContentItem();
        var issues = ZoneContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("Sid", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Item_with_max_less_than_min_fails()
    {
        var item = new ZoneContentItem { Sid = "name_mana_well", MinCount = 3, MaxCount = 1 };
        var issues = ZoneContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("MaxCount"));
    }

    [Fact]
    public void Item_with_negative_count_fails()
    {
        var item = new ZoneContentItem { Sid = "x", MinCount = -1, MaxCount = 1 };
        var issues = ZoneContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("MinCount"));
    }

    [Fact]
    public void Healthy_item_has_no_issues()
    {
        var item = new ZoneContentItem { Sid = "name_mana_well", MinCount = 1, MaxCount = 3 };
        Assert.Empty(ZoneContentItemValidator.Validate(item));
    }

    [Fact]
    public void NearCastle_with_far_road_distance_warns()
    {
        var item = new ZoneContentItem
        {
            Sid = "name_mana_well",
            NearCastle = true,
            RoadDistance = RoadDistance.Far
        };
        var issues = ZoneContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("NearCastle") && i.Contains("Far"));
    }
}
