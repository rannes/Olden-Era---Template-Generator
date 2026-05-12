using OldenEra.Generator.Services.ZoneContent;
using Xunit;

public class EmitWarningTests
{
    [Fact]
    public void Codes_BiomeFilterIgnored_IsKnownConstant()
    {
        Assert.Equal("BiomeFilter.Ignored", EmitWarning.Codes.BiomeFilterIgnored);
    }

    [Fact]
    public void Codes_FactionAffinityIgnored_IsKnownConstant()
    {
        Assert.Equal("FactionAffinity.Ignored", EmitWarning.Codes.FactionAffinityIgnored);
    }

    [Fact]
    public void Codes_PoolNonMandatoryDropped_IsKnownConstant()
    {
        Assert.Equal("Pool.NonMandatoryDropped", EmitWarning.Codes.PoolNonMandatoryDropped);
    }

    [Fact]
    public void Codes_MinCountRangeNarrowedToMax_IsKnownConstant()
    {
        Assert.Equal("MinCount.RangeNarrowedToMax", EmitWarning.Codes.MinCountRangeNarrowedToMax);
    }
}
