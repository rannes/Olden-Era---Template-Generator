using System.Linq;
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

    [Fact]
    public void Each_entry_has_non_empty_sid_and_category()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Sid));
            Assert.False(string.IsNullOrWhiteSpace(e.Category));
        });
    }

    [Fact]
    public void Sids_are_unique()
    {
        var entries = ZoneContentSidCatalog.All();
        var duplicates = entries
            .GroupBy(e => e.Sid)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicates);
    }
}
