using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

/// <summary>
/// Pure logic for selecting which release asset to download and parsing release tags.
/// Kept WPF-free so it can be unit-tested without referencing the editor project directly.
/// </summary>
public static class UpdateAssetSelection
{
    // Matches OldenEraTemplateGenerator-v{anything}-win-x64.exe (case-insensitive).
    private static readonly Regex AssetPattern = new(
        @"^OldenEraTemplateGenerator-v.*-win-x64\.exe$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a release tag like "v1.2", "1.2", or "v1.2.3" into a Version.
    /// Returns null on garbage.
    /// </summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        string trimmed = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(trimmed, out var v) ? v : null;
    }

    /// <summary>
    /// From a list of asset names, picks the one we can install. Prefers an exact
    /// version-string match when multiple assets match the pattern.
    /// </summary>
    public static string? SelectAsset(IEnumerable<string> assetNames, Version targetVersion)
    {
        if (assetNames is null) return null;

        string longMarker = $"-v{targetVersion.Major}.{targetVersion.Minor}.{Math.Max(0, targetVersion.Build)}-";
        string shortMarker = targetVersion.Build < 0
            ? $"-v{targetVersion.Major}.{targetVersion.Minor}-"
            : null!;

        string? longMatch  = null;
        string? shortMatch = null;
        string? firstMatch = null;

        foreach (var name in assetNames)
        {
            if (name is null || !AssetPattern.IsMatch(name)) continue;
            firstMatch ??= name;
            if (longMatch is null && name.Contains(longMarker, StringComparison.OrdinalIgnoreCase))
                longMatch = name;
            else if (shortMarker != null && shortMatch is null && name.Contains(shortMarker, StringComparison.OrdinalIgnoreCase))
                shortMatch = name;
        }
        return longMatch ?? shortMatch ?? firstMatch;
    }
}
