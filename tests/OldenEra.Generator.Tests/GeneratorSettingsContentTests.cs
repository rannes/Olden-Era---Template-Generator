using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class GeneratorSettingsContentTests
{
    [Fact]
    public void New_settings_have_empty_zone_content_lists()
    {
        var s = new GeneratorSettings();
        Assert.NotNull(s.PlayerZoneContent);
        Assert.Empty(s.PlayerZoneContent.Items);
        Assert.NotNull(s.NeutralZoneContent);
        Assert.Empty(s.NeutralZoneContent.Global.Items);
        Assert.NotNull(s.ContentConnectionRules);
        Assert.Empty(s.ContentConnectionRules);
    }
}
