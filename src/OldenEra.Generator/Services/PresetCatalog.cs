using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services;

public sealed record PresetEntry(string Id, string Name, string Description, string File);

public sealed class PresetCatalog
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions SettingsOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ManifestResource = "OldenEra.Generator.Resources.Presets.presets.json";
    private const string PresetResourcePrefix = "OldenEra.Generator.Resources.Presets.";

    private readonly Assembly _assembly;
    private readonly Dictionary<string, PresetEntry> _byId;

    public IReadOnlyList<PresetEntry> Entries { get; }

    public PresetCatalog() : this(typeof(PresetCatalog).Assembly) { }

    internal PresetCatalog(Assembly assembly)
    {
        _assembly = assembly;
        using var stream = _assembly.GetManifestResourceStream(ManifestResource)
            ?? throw new InvalidOperationException(
                $"Embedded manifest '{ManifestResource}' was not found. " +
                $"Check that Resources/Presets/presets.json is included as EmbeddedResource.");

        var entries = JsonSerializer.Deserialize<List<PresetEntry>>(stream, ManifestOptions)
            ?? new List<PresetEntry>();

        Entries = entries;
        _byId = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    public SettingsFile Load(string id)
    {
        if (!_byId.TryGetValue(id, out var entry))
            throw new KeyNotFoundException($"No preset with id '{id}'.");

        var resourceName = PresetResourcePrefix + entry.File;
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded preset '{resourceName}' was not found.");

        var settings = JsonSerializer.Deserialize<SettingsFile>(stream, SettingsOptions)
            ?? throw new InvalidDataException($"Preset '{id}' deserialized to null.");

        return settings;
    }
}
