using System.Reflection;

namespace OldenEra.Generator.Services;

/// <summary>
/// T-803: inline field help. Loads <c>docs/field-help.yaml</c> (embedded into
/// this assembly) at startup and exposes a single-line help string per field
/// id. UI hosts call <see cref="For"/> to populate Web <c>title=</c> attributes
/// and WPF <c>ToolTip</c> values; both render the same text.
/// </summary>
/// <remarks>
/// The catalog is intentionally tolerant: a missing manifest, malformed line,
/// or unknown key all return <c>null</c> so the host renders no tooltip and
/// default behavior is unchanged. Keys mirror
/// <see cref="SettingsValidator.ValidationFieldKeys"/> ids where one already
/// exists; documentation-only keys live on <see cref="FieldHelpKeys"/> and
/// follow the same dotted shape.
/// </remarks>
public sealed class FieldHelpCatalog
{
    public const string ManifestResource = "OldenEra.Generator.Resources.Docs.field-help.yaml";

    private static readonly Lazy<FieldHelpCatalog> _default = new(() => new FieldHelpCatalog());

    /// <summary>Process-wide default catalog backed by the embedded YAML asset.</summary>
    public static FieldHelpCatalog Default => _default.Value;

    private readonly IReadOnlyDictionary<string, string> _entries;

    public FieldHelpCatalog() : this(LoadFromAssembly(typeof(FieldHelpCatalog).Assembly)) { }

    /// <summary>Test seam: build a catalog from explicit entries.</summary>
    public FieldHelpCatalog(IReadOnlyDictionary<string, string> entries)
    {
        _entries = entries ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Number of documented fields.</summary>
    public int Count => _entries.Count;

    /// <summary>All key/value pairs (for tests + tooling).</summary>
    public IEnumerable<KeyValuePair<string, string>> Entries => _entries;

    /// <summary>
    /// Look up the help text for <paramref name="key"/>. Returns <c>null</c>
    /// when the key is unknown — callers must treat null as "no tooltip" and
    /// emit no <c>title=</c>/<c>ToolTip</c> attribute.
    /// </summary>
    public string? For(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _entries.TryGetValue(key, out var value) ? value : null;
    }

    private static IReadOnlyDictionary<string, string> LoadFromAssembly(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream(ManifestResource);
        if (stream is null) return new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// Parse the tiny YAML subset we use: blank lines and `#`-comments are
    /// skipped; every other line must be `key: value`. Quotes around the
    /// value are optional and stripped if balanced. Indentation, anchors,
    /// flow style, and multi-line scalars are not supported — fail-soft: a
    /// malformed line is dropped and parsing continues.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(yaml)) return result;

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0) continue;

            // Strip a single layer of balanced quotes if present.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            result[key] = value;
        }
        return result;
    }
}

/// <summary>
/// T-803: documentation-only field ids that do not have a matching
/// validation rule. These extend
/// <see cref="SettingsValidator.ValidationFieldKeys"/> for fields the
/// validator never flags but that still benefit from inline help.
/// </summary>
public static class FieldHelpKeys
{
    public const string Seed = "seed";
    public const string PlayerZoneSize = "zones.player.size";
    public const string NeutralZoneSize = "zones.neutral.size";
    public const string GuardsRandomization = "guards.randomization";
    public const string GuardsMultiplier = "guards.multiplier";
    public const string ConnectionGuardRandomization = "connection.guardRandomization";
    public const string ConnectionDefaultsGuardRandomization = "connection.defaults.guardRandomization";
    public const string HeroLighting = "hero.lighting";
    public const string HeroLightingDay = "hero.lightingDay";
    public const string GameRulesCityHold = "gameRules.cityHold";
    public const string GameRulesVictoryCondition = "gameRules.victoryCondition";
    public const string ZoneContentIncludeLists = "zoneContent.includeLists";
    public const string ZoneContentDensity = "zoneContent.density";
    public const string BordersWater = "borders.water";
    public const string BordersNoise = "borders.noise";
    public const string RoadsStyle = "roads.style";
    public const string RoadsDensity = "roads.density";
    public const string EncounterHolesEnabled = "encounterHoles.enabled";
    public const string EncounterHolesAffectedEncounters = "encounterHoles.affectedEncounters";
    public const string EncounterHolesTwoHoleEncounters = "encounterHoles.twoHoleEncounters";
    public const string ZoneOverridesGuardRandomization = "zoneOverrides.guardRandomization";
    public const string ZoneOverridesGuardMultiplier = "zoneOverrides.guardMultiplier";
    public const string PreviewZoom = "preview.zoom";
    public const string PresetLoad = "preset.load";
}
