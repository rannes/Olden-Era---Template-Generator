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

        string exactMarker1 = $"-v{targetVersion.Major}.{targetVersion.Minor}.{targetVersion.Build}-";
        string exactMarker2 = $"-v{targetVersion.Major}.{targetVersion.Minor}-";

        string? firstMatch = null;
        foreach (var name in assetNames)
        {
            if (name is null || !AssetPattern.IsMatch(name)) continue;
            if (name.Contains(exactMarker1, StringComparison.OrdinalIgnoreCase)
                || (targetVersion.Build <= 0 && name.Contains(exactMarker2, StringComparison.OrdinalIgnoreCase)))
            {
                return name;
            }
            firstMatch ??= name;
        }
        return firstMatch;
    }
}
