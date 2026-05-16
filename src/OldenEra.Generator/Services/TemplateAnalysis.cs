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
/// Designed as a <c>partial class</c> so each Phase-7 task can land its own
/// method in a sibling partial file without touching this one — keeping the
/// per-task PRs merge-conflict-free.
/// </para>
/// </remarks>
public static partial class TemplateAnalysis
{
    // -- T-703 — Content-pool sanity warnings ---------------------------- (reserved)
    // -- T-704 — Per-player fairness audit ------------------------------- (TemplateAnalysis.Fairness.cs)
    // -- T-705 — Topology graph stats ------------------------------------ (TemplateAnalysis.Topology.cs)
}
