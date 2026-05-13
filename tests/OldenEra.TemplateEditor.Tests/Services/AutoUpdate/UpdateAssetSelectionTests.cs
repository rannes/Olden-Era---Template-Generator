using System;
using OldenEra.TemplateEditor.Services.AutoUpdate;
using Xunit;

namespace OldenEra.TemplateEditor.Tests.Services.AutoUpdate;

public class UpdateAssetSelectionTests
{
    [Theory]
    [InlineData("v1.2", 1, 2, -1)]
    [InlineData("V1.2", 1, 2, -1)]
    [InlineData("1.2", 1, 2, -1)]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("v0.6.7", 0, 6, 7)]
    public void ParseTag_acceptsKnownFormats(string tag, int major, int minor, int build)
    {
        var v = UpdateAssetSelection.ParseTag(tag);
        Assert.NotNull(v);
        Assert.Equal(major, v!.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(build, v.Build);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("v")]
    [InlineData("vNotANumber")]
    public void ParseTag_rejectsGarbage(string? tag)
    {
        Assert.Null(UpdateAssetSelection.ParseTag(tag));
    }

    [Fact]
    public void SelectAsset_returnsNull_whenNoMatches()
    {
        var picked = UpdateAssetSelection.SelectAsset(
            new[] { "SomethingElse-v0.7.0.exe", "OldenEra-foo.zip" },
            new Version(0, 7, 0));
        Assert.Null(picked);
    }

    [Fact]
    public void SelectAsset_picksMatchingExeName()
    {
        var picked = UpdateAssetSelection.SelectAsset(
            new[] { "OldenEraTemplateGenerator-v0.7.0-win-x64.exe" },
            new Version(0, 7, 0));
        Assert.Equal("OldenEraTemplateGenerator-v0.7.0-win-x64.exe", picked);
    }

    [Fact]
    public void SelectAsset_isCaseInsensitive()
    {
        var picked = UpdateAssetSelection.SelectAsset(
            new[] { "OLDENERATEMPLATEGENERATOR-V0.7.0-WIN-X64.EXE" },
            new Version(0, 7, 0));
        Assert.NotNull(picked);
    }

    [Fact]
    public void SelectAsset_prefersExactVersionMatch_whenMultipleCandidates()
    {
        var picked = UpdateAssetSelection.SelectAsset(
            new[]
            {
                "OldenEraTemplateGenerator-v0.6.9-win-x64.exe",
                "OldenEraTemplateGenerator-v0.7.0-win-x64.exe",
            },
            new Version(0, 7, 0));
        Assert.Equal("OldenEraTemplateGenerator-v0.7.0-win-x64.exe", picked);
    }

    [Fact]
    public void SelectAsset_skipsZipAssets()
    {
        var picked = UpdateAssetSelection.SelectAsset(
            new[] { "OldenEraTemplateGenerator-v0.7.0-win-x64.zip" },
            new Version(0, 7, 0));
        Assert.Null(picked);
    }
}
