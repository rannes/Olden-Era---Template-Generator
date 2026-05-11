using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class PresetCatalogTests
{
    [Fact]
    public void Entries_AreReadFromEmbeddedManifest()
    {
        var catalog = new PresetCatalog();

        Assert.NotEmpty(catalog.Entries);
        Assert.Contains(catalog.Entries, e => e.Id == "_test-stub");
    }

    [Fact]
    public void Load_ReturnsDeserializedSettingsFile()
    {
        var catalog = new PresetCatalog();

        var settings = catalog.Load("_test-stub");

        Assert.NotNull(settings);
        Assert.Equal("Test Stub", settings.TemplateName);
        Assert.Equal(42, settings.Seed);
    }

    [Fact]
    public void Load_ThrowsForUnknownId()
    {
        var catalog = new PresetCatalog();

        Assert.Throws<KeyNotFoundException>(() => catalog.Load("does-not-exist"));
    }

    [Fact]
    public void EveryManifestEntry_LoadsSuccessfully()
    {
        var catalog = new PresetCatalog();

        foreach (var entry in catalog.Entries)
        {
            var settings = catalog.Load(entry.Id);
            Assert.NotNull(settings);
        }
    }
}
