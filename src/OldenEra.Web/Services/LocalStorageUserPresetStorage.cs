using System.Text.Json;
using Microsoft.JSInterop;
using OldenEra.Generator.Services;

namespace OldenEra.Web.Services;

/// <summary>
/// Browser-localStorage backing for <see cref="UserPresetStore"/>. T-807.
///
/// All user presets live under a single key (<see cref="StorageKey"/>) as a
/// JSON map of <c>{name: settings-json-string}</c>. Storing one key keeps the
/// number of localStorage entries bounded (browsers cap quota per origin) and
/// makes listing a single fetch.
///
/// All interop is wrapped in try/catch — Safari private mode and locked-down
/// enterprise profiles can throw on access. We degrade silently rather than
/// blocking the UI, matching <see cref="BrowserSettingsStore"/>'s contract.
/// </summary>
public sealed class LocalStorageUserPresetStorage : IUserPresetStorage
{
    public const string StorageKey = "oe-user-presets";

    private readonly IJSRuntime _js;

    public LocalStorageUserPresetStorage(IJSRuntime js) => _js = js;

    public async Task<IReadOnlyList<string>> ListNamesAsync()
    {
        var map = await ReadMapAsync();
        return map.Keys.ToList();
    }

    public async Task<string?> ReadAsync(string name)
    {
        var map = await ReadMapAsync();
        return map.TryGetValue(name, out var json) ? json : null;
    }

    public async Task WriteAsync(string name, string json)
    {
        var map = await ReadMapAsync();
        map[name] = json;
        await WriteMapAsync(map);
    }

    public async Task DeleteAsync(string name)
    {
        var map = await ReadMapAsync();
        if (map.Remove(name))
            await WriteMapAsync(map);
    }

    private async Task<Dictionary<string, string>> ReadMapAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(raw))
                return new Dictionary<string, string>(StringComparer.Ordinal);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(raw)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private async Task WriteMapAsync(Dictionary<string, string> map)
    {
        try
        {
            var json = JsonSerializer.Serialize(map);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // Storage may be disabled or full — silently ignore so the UI keeps working.
        }
    }
}
