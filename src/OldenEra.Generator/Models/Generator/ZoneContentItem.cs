using System.Collections.Generic;

namespace OldenEra.Generator.Models
{
    public sealed class ZoneContentItem
    {
        public string Sid { get; set; } = "";
        public string? Handle { get; set; }
        public bool IsGroup { get; set; }
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; } = 1;
        public ZoneContentPool Pool { get; set; } = ZoneContentPool.Mandatory;
        public bool IsGuarded { get; set; }
        public bool NearCastle { get; set; }
        public RoadDistance? RoadDistance { get; set; }
        public List<string> FactionAffinity { get; set; } = new();
        public List<string> BiomeFilter { get; set; } = new();

        /// <summary>
        /// Explicit <c>ContentItem.rules</c> entries (T-202). The convenience
        /// flags <see cref="NearCastle"/> and <see cref="RoadDistance"/> still
        /// auto-emit their own rules; entries here are appended afterwards so
        /// authors can pin scenario-style placement constraints (Crossroads,
        /// Border, custom MainObject indices, etc.) the flags don't cover.
        /// Empty list = no extra rules emitted. Mirrors the .rmg.json shape:
        /// <c>{ "type": "...", "args": [...], "targetMin": ..., "targetMax": ..., "weight": ... }</c>.
        /// </summary>
        public List<ZoneContentRule> Rules { get; set; } = new();
    }

    /// <summary>One row of <see cref="ZoneContentItem.Rules"/> — mirrors the
    /// shape of <c>OldenEra.Generator.Models.Unfrozen.ContentPlacementRule</c>
    /// in the user-authored settings tree. Kept separate from the schema type
    /// so settings models stay free of <c>System.Text.Json</c> attributes.</summary>
    public sealed class ZoneContentRule
    {
        /// <summary>Position-anchor type ("MainObject", "Road", "Crossroads", "Border", …).
        /// Empty / null → row dropped on emit.</summary>
        public string? Type { get; set; }
        /// <summary>Type-specific args. For <c>MainObject</c> this is the main-object
        /// index ("0", "1"); for <c>Road</c>/<c>Crossroads</c>/<c>Border</c> commonly empty.</summary>
        public List<string> Args { get; set; } = new();
        /// <summary>Lower fraction bound (0.0–1.0). null = field omitted on emit.</summary>
        public double? TargetMin { get; set; }
        /// <summary>Upper fraction bound (0.0–1.0). null = field omitted on emit.</summary>
        public double? TargetMax { get; set; }
        /// <summary>Relative weight when multiple rules compete. null = field omitted.</summary>
        public double? Weight { get; set; }
    }
}
