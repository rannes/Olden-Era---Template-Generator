using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-807: round-trip semantics for the user-preset store. Drives the abstraction
/// with an in-memory storage so the test runs identically on every host.
/// </summary>
public class UserPresetStoreTests
{
    private sealed class InMemoryStorage : IUserPresetStorage
    {
        public readonly Dictionary<string, string> Map =
            new(StringComparer.Ordinal);

        public Task<IReadOnlyList<string>> ListNamesAsync()
            => Task.FromResult<IReadOnlyList<string>>(new List<string>(Map.Keys));

        public Task<string?> ReadAsync(string name)
            => Task.FromResult(Map.TryGetValue(name, out var v) ? v : null);

        public Task WriteAsync(string name, string json)
        {
            Map[name] = json;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name)
        {
            Map.Remove(name);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SaveLoad_RoundTrips_SettingsIdentity()
    {
        var storage = new InMemoryStorage();
        var store = new UserPresetStore(storage);

        var settings = new SettingsFile
        {
            TemplateName = "Round-Trip Test",
            MapSize = 240,
            PlayerCount = 4,
            NeutralZoneCount = 7,
        };

        await store.SaveAsync("My Map", settings);
        var loaded = await store.LoadAsync("My Map");

        Assert.NotNull(loaded);
        Assert.Equal("Round-Trip Test", loaded!.TemplateName);
        Assert.Equal(240, loaded.MapSize);
        Assert.Equal(4, loaded.PlayerCount);
        Assert.Equal(7, loaded.NeutralZoneCount);
    }

    [Fact]
    public async Task ListAsync_SortsCaseInsensitivelyAndReflectsSaves()
    {
        var store = new UserPresetStore(new InMemoryStorage());
        await store.SaveAsync("zeta", new SettingsFile());
        await store.SaveAsync("alpha", new SettingsFile());
        await store.SaveAsync("Beta", new SettingsFile());

        var entries = await store.ListAsync();
        Assert.Equal(new[] { "alpha", "Beta", "zeta" },
            entries.Select(e => e.Name).ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var store = new UserPresetStore(new InMemoryStorage());
        await store.SaveAsync("temp", new SettingsFile());
        Assert.Single(await store.ListAsync());

        await store.DeleteAsync("temp");
        Assert.Empty(await store.ListAsync());
        Assert.Null(await store.LoadAsync("temp"));
    }

    [Fact]
    public async Task SaveAsync_RejectsEmptyName()
    {
        var store = new UserPresetStore(new InMemoryStorage());
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAsync("   ", new SettingsFile()));
    }

    [Fact]
    public async Task SaveAsync_RejectsTooLongName()
    {
        var store = new UserPresetStore(new InMemoryStorage());
        var huge = new string('a', UserPresetStore.MaxNameLength + 1);
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAsync(huge, new SettingsFile()));
    }

    [Fact]
    public void NormalizeName_TrimsAndClamps()
    {
        Assert.Equal("hi", UserPresetStore.NormalizeName("  hi  "));
        Assert.Equal(string.Empty, UserPresetStore.NormalizeName("   "));
        Assert.Equal(string.Empty, UserPresetStore.NormalizeName(null));
        var huge = new string('x', UserPresetStore.MaxNameLength + 5);
        Assert.Equal(UserPresetStore.MaxNameLength,
            UserPresetStore.NormalizeName(huge).Length);
    }
}
