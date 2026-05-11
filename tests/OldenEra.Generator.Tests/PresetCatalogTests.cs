using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class PresetCatalogTests
{
    [Fact]
    public void Entries_AreReadFromEmbeddedManifest()
    {
        var catalog = new PresetCatalog();
        Assert.Equal(3, catalog.Entries.Count);
        Assert.Contains(catalog.Entries, e => e.Id == "jebus-like");
        Assert.Contains(catalog.Entries, e => e.Id == "arcade-2v2");
        Assert.Contains(catalog.Entries, e => e.Id == "big-map-ffa");
    }

    [Fact]
    public void Load_ReturnsDeserializedSettingsFile()
    {
        var catalog = new PresetCatalog();
        var settings = catalog.Load("jebus-like");
        Assert.NotNull(settings);
        Assert.Equal("Jebus-like", settings.TemplateName);
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
