using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentPresetsTests
{
    [Fact]
    public void Presets_are_nonempty_and_valid()
    {
        var presets = ZoneContentPresets.All();
        Assert.NotEmpty(presets);
        Assert.All(presets, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.NotNull(p.Item);
            Assert.Empty(ZoneContentItemValidator.Validate(p.Item));
        });
    }
}
