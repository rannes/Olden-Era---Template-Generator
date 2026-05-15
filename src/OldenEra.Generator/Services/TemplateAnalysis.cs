using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services;

/// <summary>
/// Phase-7 read-only analytics over a generated <see cref="RmgTemplate"/>.
/// Strictly pure display: every method reads already-emitted fields and
/// reformats them for the UI. No simulation, no recomputation of guard math,
/// no mutation.
/// </summary>
/// <remarks>
/// <para>
/// Designed as a <c>partial class</c> so each Phase-7 task (T-701 value budget,
/// T-702 guard-power vs. value, T-704 fairness audit, T-705 topology graph stats)
/// can land its own method in a sibling partial file without touching this one
/// — keeping the per-task PRs merge-conflict-free.
/// </para>
/// <para>
/// File regions in this base file are reserved by task number. Tasks that need
/// significant supporting types should add a new partial file
/// (<c>TemplateAnalysis.&lt;Topic&gt;.cs</c>) rather than crowding this one.
/// </para>
/// </remarks>
public static partial class TemplateAnalysis
{
    // -- T-701 — Zone value budget ---------------------------------------

    /// <summary>
    /// Per-zone snapshot of the six value knobs the generator emits on
    /// <see cref="Zone"/>: guarded / unguarded / resources, each in scalar
    /// and per-area form. All fields are nullable so the UI can render an
    /// em-dash for omissions instead of misleading zeros.
    /// </summary>
    public sealed record ZoneValueBudget(
        string ZoneName,
        int? ResourcesValue,
        int? ResourcesValuePerArea,
        int? GuardedContentValue,
        int? GuardedContentValuePerArea,
        int? UnguardedContentValue,
        int? UnguardedContentValuePerArea);

    /// <summary>
    /// Sum-of-scalars summary across every zone in every variant of the
    /// template. Per-area numbers are not summed — they are densities, not
    /// extensive quantities — so totals only carry the scalar columns.
    /// </summary>
    public sealed record ValueBudgetTotals(
        int ResourcesValue,
        int GuardedContentValue,
        int UnguardedContentValue)
    {
        public int Combined => ResourcesValue + GuardedContentValue + UnguardedContentValue;
    }

    /// <summary>
    /// Result of <see cref="ComputeValueBudget"/>. <see cref="Zones"/> is empty
    /// when the template is null or carries no variants/zones — callers should
    /// hide the panel in that case.
    /// </summary>
    public sealed record ValueBudgetReport(
        IReadOnlyList<ZoneValueBudget> Zones,
        ValueBudgetTotals Totals)
    {
        public bool HasData => Zones.Count > 0;
    }

    /// <summary>
    /// Reads <see cref="Zone.ResourcesValue"/>, <see cref="Zone.GuardedContentValue"/>,
    /// <see cref="Zone.UnguardedContentValue"/> and their per-area variants off
    /// the template and returns a per-zone + totals report. No defaults invented.
    /// </summary>
    /// <remarks>
    /// Variants are flattened: in shipped templates only one variant ships, so
    /// the typical caller sees one row per logical zone. If a future template
    /// emits multiple variants, each variant's zones contribute their own rows
    /// — duplicates are not deduped because the variants can disagree.
    /// </remarks>
    public static ValueBudgetReport ComputeValueBudget(RmgTemplate? template)
    {
        if (template?.Variants is null || template.Variants.Count == 0)
            return Empty();

        var rows = new List<ZoneValueBudget>();
        int totalResources = 0;
        int totalGuarded = 0;
        int totalUnguarded = 0;

        foreach (var variant in template.Variants)
        {
            if (variant.Zones is null) continue;
            foreach (var zone in variant.Zones)
            {
                rows.Add(new ZoneValueBudget(
                    ZoneName: string.IsNullOrEmpty(zone.Name) ? "(unnamed)" : zone.Name,
                    ResourcesValue: zone.ResourcesValue,
                    ResourcesValuePerArea: zone.ResourcesValuePerArea,
                    GuardedContentValue: zone.GuardedContentValue,
                    GuardedContentValuePerArea: zone.GuardedContentValuePerArea,
                    UnguardedContentValue: zone.UnguardedContentValue,
                    UnguardedContentValuePerArea: zone.UnguardedContentValuePerArea));

                totalResources += zone.ResourcesValue ?? 0;
                totalGuarded += zone.GuardedContentValue ?? 0;
                totalUnguarded += zone.UnguardedContentValue ?? 0;
            }
        }

        return new ValueBudgetReport(
            Zones: rows,
            Totals: new ValueBudgetTotals(totalResources, totalGuarded, totalUnguarded));
    }

    private static ValueBudgetReport Empty() =>
        new(System.Array.Empty<ZoneValueBudget>(), new ValueBudgetTotals(0, 0, 0));

    // -- T-702 — Guard-power vs. value ----------------------------------- (reserved)
    // -- T-703 — Content-pool sanity warnings ---------------------------- (reserved)
    // -- T-704 — Per-player fairness audit ------------------------------- (reserved)
    // -- T-705 — Topology graph stats ------------------------------------ (reserved)
}
