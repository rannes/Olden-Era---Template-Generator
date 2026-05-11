using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentSidCatalogTests
{
    [Fact]
    public void Catalog_contains_known_mana_well_sid()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.Contains(entries, e => e.Sid == "name_mana_well");
    }

    [Fact]
    public void Each_entry_has_friendly_name()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.FriendlyName)));
    }
}
