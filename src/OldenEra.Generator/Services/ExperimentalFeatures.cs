using System;
using System.Collections.Generic;

namespace OldenEra.Generator.Services;

/// <summary>
/// T-302 — registry of the per-feature experimental flags surfaced on
/// <see cref="OldenEra.Generator.Models.SettingsFile"/>. Defines the feature
/// keys, their UI labels, and whether each is still flagged "experimental"
/// or has graduated to stable. Source-controlled metadata; never persisted.
/// </summary>
public enum ExperimentalStatus
{
    /// <summary>Still flagged with the ⚗ EXPERIMENTAL pill.</summary>
    Experimental,
    /// <summary>Toggle remains for backwards compat but the badge is hidden.</summary>
    Graduated,
}

/// <summary>One row of the experimental-features registry.</summary>
public sealed record ExperimentalFeature(
    string Key,
    string Title,
    string Description,
    ExperimentalStatus Status);

public static class ExperimentalFeatures
{
    public const string GameMode          = "game-mode";
    public const string StartingBonuses   = "starting-bonuses";
    public const string ZoneContent       = "zone-content";
    public const string BordersRoads      = "borders-roads";
    public const string PerTierOverrides  = "per-tier-overrides";

    /// <summary>
    /// Ordered list of the per-feature flags. UI panels iterate this so that
    /// adding a new feature only needs a registry entry plus the matching
    /// <see cref="OldenEra.Generator.Models.SettingsFile"/> bool.
    /// </summary>
    public static readonly IReadOnlyList<ExperimentalFeature> All = new[]
    {
        new ExperimentalFeature(
            GameMode,
            "Game mode",
            "Single-hero / hire-ban / desertion overrides.",
            ExperimentalStatus.Experimental),
        new ExperimentalFeature(
            StartingBonuses,
            "Starting bonuses",
            "Resources, hero stats, items, spells, and unit multipliers.",
            ExperimentalStatus.Experimental),
        new ExperimentalFeature(
            ZoneContent,
            "Zone content",
            "Per-zone biome / pool / cutoff / encounter-hole overrides.",
            ExperimentalStatus.Experimental),
        new ExperimentalFeature(
            BordersRoads,
            "Map borders & roads",
            "Border corner radius, water border, road type.",
            ExperimentalStatus.Experimental),
        new ExperimentalFeature(
            PerTierOverrides,
            "Per-tier overrides",
            "Building presets and guard progression per neutral tier.",
            ExperimentalStatus.Experimental),
    };

    public static ExperimentalFeature Get(string key) =>
        All.FirstOrDefault(f => f.Key == key)
        ?? throw new ArgumentException($"Unknown experimental feature key: {key}", nameof(key));
}
