using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services;

/// <summary>
/// One row in a settings-vs-preset diff. <see cref="FieldPath"/> is the JSON
/// property name (e.g. <c>"mapSize"</c>) so it round-trips with .oetgs files
/// and the share codec. T-805.
/// </summary>
public sealed record SettingsDiffRow(string FieldPath, string PresetValue, string CurrentValue);

/// <summary>
/// Field-level diff between a freshly-loaded preset's <see cref="SettingsFile"/>
/// and the user's current <see cref="SettingsFile"/>. T-805.
///
/// Scope: top-level scalar properties of <see cref="SettingsFile"/> only —
/// strings, numerics, bools, nullables, and enums. Lists, dictionaries, and
/// nested object properties (e.g. <c>tierLow</c>, <c>playerZoneContent</c>)
/// are skipped: a flat field-level diff is the explicit acceptance criterion,
/// and reflection-based equality on collections would either mis-report (by
/// reference, never equal) or pull in unbounded structural compares we don't
/// need for the "what did I change since loading the preset?" UX.
/// </summary>
public static class SettingsDiff
{
    /// <summary>
    /// Compare <paramref name="preset"/> vs <paramref name="current"/> field by
    /// field. Returns one row per top-level scalar property whose value
    /// differs. Order follows declaration order on <see cref="SettingsFile"/>
    /// for stable rendering.
    /// </summary>
    public static IReadOnlyList<SettingsDiffRow> Compute(SettingsFile preset, SettingsFile current)
    {
        if (preset is null) throw new ArgumentNullException(nameof(preset));
        if (current is null) throw new ArgumentNullException(nameof(current));

        var rows = new List<SettingsDiffRow>();
        foreach (var prop in typeof(SettingsFile).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (!IsScalarType(prop.PropertyType)) continue;
            // Skip [JsonIgnore] computed views (e.g. EffectiveResourceDensityPercent).
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

            object? a = prop.GetValue(preset);
            object? b = prop.GetValue(current);
            if (Equals(a, b)) continue;

            rows.Add(new SettingsDiffRow(
                FieldPath: JsonNameOf(prop),
                PresetValue: Format(a),
                CurrentValue: Format(b)));
        }
        return rows;
    }

    private static bool IsScalarType(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u.IsEnum) return true;
        if (u.IsPrimitive) return true; // bool, int, double, etc.
        if (u == typeof(string)) return true;
        if (u == typeof(decimal)) return true;
        // Lists, dictionaries, and complex objects are out of scope (T-805).
        if (typeof(IEnumerable).IsAssignableFrom(u)) return false;
        return false;
    }

    private static string JsonNameOf(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        return attr?.Name ?? prop.Name;
    }

    private static string Format(object? v)
    {
        if (v is null) return "(unset)";
        if (v is bool b) return b ? "true" : "false";
        if (v is string s) return s.Length == 0 ? "(empty)" : s;
        return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
