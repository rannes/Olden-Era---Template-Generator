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
}
