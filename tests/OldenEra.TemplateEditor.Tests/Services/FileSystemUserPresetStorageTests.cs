using System;
using System.IO;
using System.Threading.Tasks;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using OldenEra.TemplateEditor.Services;
using Xunit;

namespace OldenEra.TemplateEditor.Tests.Services;

/// <summary>
/// T-807: filesystem-backed user-preset storage round-trip and survival across
/// process restarts. Uses a temp directory so test runs are independent.
/// </summary>
public class FileSystemUserPresetStorageTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemUserPresetStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "oe-user-presets-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task SaveLoad_RoundTripsAcrossNewStorageInstance()
    {
        // Save with one storage instance, load with a fresh one — proves the
        // bytes hit disk, mirroring the "reload the app" acceptance test.
        var saver = new UserPresetStore(new FileSystemUserPresetStorage(_tempDir));
        await saver.SaveAsync("My Big Map", new SettingsFile
        {
            TemplateName = "Persisted",
            MapSize = 256,
            PlayerCount = 6,
        });

        var loader = new UserPresetStore(new FileSystemUserPresetStorage(_tempDir));
        var entries = await loader.ListAsync();
        Assert.Single(entries);
        Assert.Equal("My Big Map", entries[0].Name);

        var settings = await loader.LoadAsync("My Big Map");
        Assert.NotNull(settings);
        Assert.Equal("Persisted", settings!.TemplateName);
        Assert.Equal(256, settings.MapSize);
        Assert.Equal(6, settings.PlayerCount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        var store = new UserPresetStore(new FileSystemUserPresetStorage(_tempDir));
        await store.SaveAsync("temp", new SettingsFile());
        await store.DeleteAsync("temp");

        Assert.Empty(await store.ListAsync());
    }

    [Theory]
    [InlineData("Simple")]
    [InlineData("with spaces")]
    [InlineData("dashes-and_underscores")]
    [InlineData("punct: yes? no!")]
    [InlineData("unicode é ✓ 漢")]
    public void EncodeDecode_RoundTripsArbitraryNames(string name)
    {
        var encoded = FileSystemUserPresetStorage.Encode(name);
        var decoded = FileSystemUserPresetStorage.Decode(encoded);
        Assert.Equal(name, decoded);
    }

    [Fact]
    public async Task ListNamesAsync_ReturnsEmpty_WhenDirectoryMissing()
    {
        var storage = new FileSystemUserPresetStorage(
            Path.Combine(_tempDir, "does-not-exist"));
        Assert.Empty(await storage.ListNamesAsync());
    }
}
