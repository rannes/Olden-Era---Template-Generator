using System;
using System.Collections.Generic;

namespace OldenEra.Generator.Services;

/// <summary>
/// Shared CSV codec for per-zone catalog SID lists (T-006). The share codec
/// only round-trips scalars / strings, so we serialise as comma-separated
/// values. Tokens are trimmed; empty / whitespace-only input yields an empty
/// list. Hoisted from three near-duplicate copies in WPF, Web, and
/// SettingsMapper to a single shared helper.
/// </summary>
public static class SidCsv
{
    public static List<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            string t = p.Trim();
            if (t.Length > 0) result.Add(t);
        }
        return result;
    }

    public static string Join(IReadOnlyList<string>? list) =>
        list is { Count: > 0 } ? string.Join(",", list) : "";

    /// <summary>Trims tokens, drops empties, rejoins as a normalised CSV string.</summary>
    public static string Normalize(string? raw) => Join(Parse(raw));
}
