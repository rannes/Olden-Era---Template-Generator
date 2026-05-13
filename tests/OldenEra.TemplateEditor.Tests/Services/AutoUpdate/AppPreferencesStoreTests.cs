using System;
using System.IO;
using OldenEra.TemplateEditor.Services.AutoUpdate;
using Xunit;

namespace OldenEra.TemplateEditor.Tests.Services.AutoUpdate;

public class AppPreferencesStoreTests : IDisposable
{
    private readonly string _path;

    public AppPreferencesStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"oetg-prefs-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* swallow */ }
    }

    [Fact]
    public void Load_returnsDefaults_whenFileMissing()
    {
        var store = new JsonAppPreferencesStore(_path);
        var prefs = store.Load();
        Assert.True(prefs.CheckForUpdatesOnStartup);
    }

    [Fact]
    public void SaveThenLoad_roundTrips()
    {
        var store = new JsonAppPreferencesStore(_path);
        store.Save(new AppPreferences { CheckForUpdatesOnStartup = false });
        Assert.False(store.Load().CheckForUpdatesOnStartup);
    }

    [Fact]
    public void Load_returnsDefaults_whenFileMalformed()
    {
        File.WriteAllText(_path, "{ not valid json");
        var store = new JsonAppPreferencesStore(_path);
        var prefs = store.Load();
        Assert.True(prefs.CheckForUpdatesOnStartup);
    }
}
