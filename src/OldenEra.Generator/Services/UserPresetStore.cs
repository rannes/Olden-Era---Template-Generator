using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services;

/// <summary>
/// One named user preset. Distinct from <see cref="PresetEntry"/> (the bundled
/// catalog) — user presets live in host-local storage (browser localStorage on
/// the Web host, %LocalAppData% on WPF) and never ship inside the assembly.
/// </summary>
public sealed record UserPresetEntry(string Name);

/// <summary>
/// Host-pluggable storage for <see cref="UserPresetStore"/>. Web supplies a
/// localStorage-backed implementation; WPF supplies a filesystem-backed one.
/// Implementations only move bytes — the store handles JSON shape.
/// </summary>
public interface IUserPresetStorage
{
    Task<IReadOnlyList<string>> ListNamesAsync();
    Task<string?> ReadAsync(string name);
    Task WriteAsync(string name, string json);
    Task DeleteAsync(string name);
}

/// <summary>
/// Persists named user presets locally. T-807. Independent of
/// <see cref="PresetCatalog"/>: built-in presets live in the assembly's
/// embedded resources; user presets live in host-local storage.
/// </summary>
public sealed class UserPresetStore
{
    /// <summary>Maximum length for a user preset name (prevents pathologically
    /// long keys in localStorage / filenames).</summary>
    public const int MaxNameLength = 64;

    private static readonly JsonSerializerOptions SettingsOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IUserPresetStorage _storage;

    public UserPresetStore(IUserPresetStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>Returns user-preset names sorted case-insensitively.</summary>
    public async Task<IReadOnlyList<UserPresetEntry>> ListAsync()
    {
        var names = await _storage.ListNamesAsync();
        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new UserPresetEntry(n))
            .ToList();
    }

    public async Task<SettingsFile?> LoadAsync(string name)
    {
        ValidateName(name);
        var json = await _storage.ReadAsync(name);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<SettingsFile>(json, SettingsOptions);
    }

    public async Task SaveAsync(string name, SettingsFile settings)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(settings);
        var json = JsonSerializer.Serialize(settings, SettingsOptions);
        await _storage.WriteAsync(name, json);
    }

    public async Task DeleteAsync(string name)
    {
        ValidateName(name);
        await _storage.DeleteAsync(name);
    }

    /// <summary>
    /// Normalises a free-text user input to a stored name: trims whitespace
    /// and clamps to <see cref="MaxNameLength"/>. Returns null/empty when the
    /// input has no usable characters; callers should reject those.
    /// </summary>
    public static string NormalizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim();
        return trimmed.Length > MaxNameLength
            ? trimmed.Substring(0, MaxNameLength)
            : trimmed;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Preset name cannot be empty.", nameof(name));
        if (name.Length > MaxNameLength)
            throw new ArgumentException(
                $"Preset name cannot exceed {MaxNameLength} characters.", nameof(name));
    }
}
