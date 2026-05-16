using System;
using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services;

/// <summary>
/// T-704 — Per-player fairness audit. Sibling partial of
/// <see cref="TemplateAnalysis"/> so each analysis task can land in its
/// own file without touching siblings.
/// </summary>
/// <remarks>
/// <para>
/// "Player zone" = a <see cref="Zone"/> whose <see cref="Zone.MainObjects"/>
/// list contains an entry of <see cref="MainObject.Type"/> <c>"Spawn"</c>. The
/// slot label comes from <see cref="MainObject.Spawn"/> (e.g. <c>"Player1"</c>).
/// All metrics are read off already-emitted fields — no simulation, no guard
/// math reapplied. Pure display.
/// </para>
/// <para>
/// Deviation rule: every per-player metric is compared against the median
/// across all player zones in the same variant; a player flags when any one
/// metric deviates by more than <see cref="DefaultDeviationPercent"/> percent
/// of the median (default 20). The acceptance bullet on T-704 names that
/// exact threshold.
/// </para>
/// </remarks>
public static partial class TemplateAnalysis
{
    /// <summary>
    /// Default deviation tolerance, in percent of the median, before a player
    /// row is flagged as imbalanced. Matches the T-704 acceptance bullet
    /// ("deviate by &gt;X% from the median (X = 20)").
    /// </summary>
    public const double DefaultDeviationPercent = 20.0;

    /// <summary>
    /// Per-player slice of the fairness audit. Slot is the integer pulled
    /// from the player's spawn label (<c>"Player3"</c> → 3); when no label
    /// is present we fall back to the zone's first-seen index.
    /// </summary>
    public sealed record PlayerFairnessRow(
        int Slot,
        string ZoneName,
        int NeighborCount,
        int StartingCastleCount,
        int ResourceYield,
        bool NeighborOutlier,
        bool StartingCastleOutlier,
        bool ResourceYieldOutlier)
    {
        /// <summary>Any metric flagged → row lights up in the UI.</summary>
        public bool IsOutlier =>
            NeighborOutlier || StartingCastleOutlier || ResourceYieldOutlier;
    }

    /// <summary>
    /// Median values across the audited rows. Useful for the panel's hint
    /// row and for tests that pin the threshold math.
    /// </summary>
    public sealed record FairnessMedians(
        double NeighborCount,
        double StartingCastleCount,
        double ResourceYield);

    /// <summary>
    /// Result of <see cref="ComputeFairness"/>. Empty <see cref="Players"/>
    /// means there were no Spawn-bearing zones (or no template) — the panel
    /// hides itself in that case (the panel's hidden-when-empty pattern).
    /// </summary>
    public sealed record FairnessReport(
        IReadOnlyList<PlayerFairnessRow> Players,
        FairnessMedians Medians,
        double DeviationPercent)
    {
        public bool HasData => Players.Count > 0;
        public bool AnyOutlier => Players.Any(p => p.IsOutlier);
    }

    /// <summary>
    /// Read every player zone (one per Spawn MainObject) off the template,
    /// compute neighbor count / starting-castle count / expected resource
    /// yield, and flag rows whose any metric deviates by more than
    /// <paramref name="deviationPercent"/> percent of the median.
    /// </summary>
    /// <param name="template">Generated template; <c>null</c> → empty report.</param>
    /// <param name="deviationPercent">Tolerance in percent of the median;
    /// defaults to <see cref="DefaultDeviationPercent"/>.</param>
    public static FairnessReport ComputeFairness(
        RmgTemplate? template,
        double deviationPercent = DefaultDeviationPercent)
    {
        if (template?.Variants is null || template.Variants.Count == 0)
            return EmptyFairness(deviationPercent);

        // Use the first variant — shipped templates ship one. Fairness only
        // makes sense within a single variant since players exist within one.
        var variant = template.Variants[0];
        if (variant.Zones is null || variant.Zones.Count == 0)
            return EmptyFairness(deviationPercent);

        // Collect player zones (those carrying at least one Spawn MainObject).
        // A single zone with multiple Spawns is unusual but we still surface
        // one row per Spawn so each slot's bonuses tally separately.
        var rawRows = new List<(int slot, Zone zone)>();
        int fallbackSlot = 1;
        foreach (var zone in variant.Zones)
        {
            if (zone.MainObjects is null) continue;
            foreach (var mo in zone.MainObjects)
            {
                if (!string.Equals(mo.Type, "Spawn", StringComparison.Ordinal))
                    continue;
                int slot = ParseSpawnSlot(mo.Spawn) ?? fallbackSlot;
                rawRows.Add((slot, zone));
                fallbackSlot++;
            }
        }

        if (rawRows.Count == 0)
            return EmptyFairness(deviationPercent);

        // Pre-index connections by zone name — neighbor lookup is O(zones)
        // per player which is fine for the ~8-player ceiling but cheap to
        // make linear-by-connection regardless.
        var neighborsByZone = BuildNeighborIndex(variant.Connections);

        // Bonus tally per slot from GameRules.bonuses[]. Bonuses without a
        // ReceiverSide are global — they apply to every player equally, so
        // they cannot differentiate fairness and are skipped. Each present
        // Bonus contributes 1 unit of yield (we lack a value oracle for
        // sids); the relative count still surfaces an asymmetric loadout.
        var bonusBySlot = TallyBonusesBySlot(template);

        var rows = new List<(int slot, string name, int neighbors, int castles, int yield)>();
        foreach (var (slot, zone) in rawRows.OrderBy(r => r.slot))
        {
            int neighbors = neighborsByZone.TryGetValue(zone.Name, out var ns) ? ns.Count : 0;
            int castles = zone.MainObjects?.Count(mo =>
                string.Equals(mo.Type, "City", StringComparison.Ordinal)) ?? 0;

            int zoneYield =
                (zone.ResourcesValue ?? 0)
                + (zone.GuardedContentValue ?? 0)
                + (zone.UnguardedContentValue ?? 0);
            int bonusYield = bonusBySlot.TryGetValue(slot, out var bonusCount) ? bonusCount : 0;
            int yield = zoneYield + bonusYield;

            rows.Add((slot, zone.Name, neighbors, castles, yield));
        }

        // Median per metric, then a per-row deviation flag against it.
        double medNeighbors = Median(rows.Select(r => (double)r.neighbors));
        double medCastles = Median(rows.Select(r => (double)r.castles));
        double medYield = Median(rows.Select(r => (double)r.yield));

        var players = rows.Select(r => new PlayerFairnessRow(
            Slot: r.slot,
            ZoneName: string.IsNullOrEmpty(r.name) ? "(unnamed)" : r.name,
            NeighborCount: r.neighbors,
            StartingCastleCount: r.castles,
            ResourceYield: r.yield,
            NeighborOutlier: IsOutlier(r.neighbors, medNeighbors, deviationPercent),
            StartingCastleOutlier: IsOutlier(r.castles, medCastles, deviationPercent),
            ResourceYieldOutlier: IsOutlier(r.yield, medYield, deviationPercent)))
            .ToList();

        return new FairnessReport(
            Players: players,
            Medians: new FairnessMedians(medNeighbors, medCastles, medYield),
            DeviationPercent: deviationPercent);
    }

    private static FairnessReport EmptyFairness(double deviationPercent) =>
        new(
            Players: Array.Empty<PlayerFairnessRow>(),
            Medians: new FairnessMedians(0, 0, 0),
            DeviationPercent: deviationPercent);

    /// <summary>
    /// Parse a Spawn slot label (<c>"Player3"</c> → 3). Returns null when
    /// the label is empty / unparseable; the caller falls back to a
    /// running index.
    /// </summary>
    private static int? ParseSpawnSlot(string? spawn)
    {
        if (string.IsNullOrEmpty(spawn)) return null;
        // Spawns use the canonical Player1..Player8 form (KnownValues.SpawnPlayers).
        const string prefix = "Player";
        if (!spawn.StartsWith(prefix, StringComparison.Ordinal)) return null;
        return int.TryParse(spawn.AsSpan(prefix.Length), out int n) ? n : null;
    }

    /// <summary>
    /// Build an index <c>zoneName → distinct neighbor zone names</c> from
    /// the variant's connections. Self-loops are skipped; duplicate edges
    /// dedupe so the count reflects distinct neighbors, not raw connection
    /// count.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildNeighborIndex(
        IList<Connection>? connections)
    {
        var idx = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (connections is null) return idx;
        foreach (var c in connections)
        {
            if (string.IsNullOrEmpty(c.From) || string.IsNullOrEmpty(c.To)) continue;
            if (string.Equals(c.From, c.To, StringComparison.Ordinal)) continue;
            AddNeighbor(idx, c.From, c.To);
            AddNeighbor(idx, c.To, c.From);
        }
        return idx;
    }

    private static void AddNeighbor(
        Dictionary<string, HashSet<string>> idx, string a, string b)
    {
        if (!idx.TryGetValue(a, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            idx[a] = set;
        }
        set.Add(b);
    }

    /// <summary>
    /// Tally Bonus entries by their <see cref="Bonus.ReceiverSide"/>.
    /// Global bonuses (no ReceiverSide) are skipped because they cannot
    /// differentiate slots. Each bonus contributes 1 to its slot's count.
    /// </summary>
    private static Dictionary<int, int> TallyBonusesBySlot(RmgTemplate template)
    {
        var tally = new Dictionary<int, int>();
        var bonuses = template.GameRules?.Bonuses;
        if (bonuses is null) return tally;
        foreach (var b in bonuses)
        {
            if (b.ReceiverSide is not int slot) continue;
            tally.TryGetValue(slot, out var count);
            tally[slot] = count + 1;
        }
        return tally;
    }

    private static double Median(IEnumerable<double> source)
    {
        var sorted = source.OrderBy(x => x).ToArray();
        if (sorted.Length == 0) return 0;
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    /// <summary>
    /// True when <paramref name="value"/> deviates from the median by more
    /// than <paramref name="percent"/>% of the median. Special case: if the
    /// median is 0, any non-zero value is an outlier (otherwise the rule
    /// would silently pass an asymmetric "0 vs 5" split).
    /// </summary>
    private static bool IsOutlier(double value, double median, double percent)
    {
        if (median <= 0)
            return value > 0;
        double tolerance = median * (percent / 100.0);
        return Math.Abs(value - median) > tolerance;
    }
}
