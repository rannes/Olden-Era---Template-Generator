# Balanced Map Topology — Design

**Date:** 2026-05-13
**Status:** Deferred initiative; design ready, no implementation branch yet.
**Upstream commits covered:** `6dd070e`, `0e31576`, `3f46209`, `6d28ef0`,
`3c4fbf8`, `460d2c7`, `721ac24`, `6ad5250`, `90b09b6`, `48b731d` (10 commits;
single feature arc).

## Overview

Upstream introduced a new `MapTopology.Balanced` value: a tournament-style
layout where player spawn zones are placed on concentric rings around the
map center, and the preview snaps zone positions to those rings for a
visually balanced result. We will port the feature behind the existing
master experimental toggle and **not** flip it on by default.

## Goals

1. Add `MapTopology.Balanced` to the shared library, additive — keep
   `Default` and `Random` unchanged.
2. Implement balanced spawn placement (`BuildVariantBalanced`,
   `BuildBalancedRingLetters`, `BuildBalancedRandomPositions`) and the
   tournament variant (`BuildTournamentBalancedCluster`) in
   `TemplateGenerator.cs`, threaded through our seeded `Random` so output
   is deterministic.
3. Implement the ring-snap pass in `TemplatePreviewRenderer.cs`, gated on
   `topology == Balanced`. Adopt upstream's "Random = no ring-snap, ever"
   semantic — matches our current behavior.
4. Surface a "Balanced" option in both the WPF and Web topology selector,
   visible only when the master Experimental toggle is on.
5. Deprecate `GeneratorSettings.ExperimentalBalancedZonePlacement` but keep
   the JSON property readable for back-compat.

## Out of scope

- Flipping the default topology. Upstream's `6dd070e` does this; we will not.
- Per-faction tournament cluster tuning beyond what upstream ships.
- New experimental panels — Balanced is a topology choice, not its own panel.

## Decisions log

- **(a) Gate behind master Experimental toggle** rather than ship as a
  first-class topology. Matches the post-process pattern documented in
  `docs/plans/2026-05-10-experimental-features-rollout.md`. If user
  feedback is positive we promote later.
- **(b) Reimplement, don't cherry-pick.** `TemplateGenerator.cs` and
  `TemplatePreviewRenderer.cs` have diverged enough (seeded `Random`
  `a6ce16b`, `KnownIds` `824bad2`, zone-content rounds, FR spring layout,
  bridge fade) that every upstream hunk in those files conflicts. Read the
  upstream diffs, write equivalent code on top of current `main`.
- **(c) Determinism.** `BuildBalancedRandomPositions` takes the existing
  `Random` instance. Same seed → same balanced layout, same as we already
  guarantee for `Random` topology.
- **(d) Ring-snap ships in the shared library** so both Web and WPF
  previews get it. Gated on `topology == Balanced`.
- **(e) Random topology gets the new "no ring-snap, ever" guarantee.**
  Matches `3f46209`'s intent; matches our renderer's current early return at
  `TemplatePreviewRenderer.cs` (verify line on impl).
- **(f) Settings back-compat.** Mark `ExperimentalBalancedZonePlacement`
  `[Obsolete]` but keep the property + JSON serialization so old `.oetgs`
  files load. New code reads `MapTopology`.

## Conflict map

| File | Divergence | Strategy |
| --- | --- | --- |
| `MapTopology.cs` | None | Additive enum value. |
| `TemplateGenerator.cs` | High (seeded `Random`, zone content #17, `KnownIds`) | Reimplement balanced branches; thread `Random`. |
| `TemplatePreviewRenderer.cs` | High (renamed from `TemplatePreviewPngWriter.cs`; FR layout + bridge fade) | Reimplement ring-snap pass. |
| `GeneratorSettings.cs` | Diverged but additive-safe | Remove `ExperimentalBalancedZonePlacement` (no longer consumed at runtime). |
| `SettingsFile.cs` / `SettingsMapper` | Additive | Round-trip the topology choice; nothing new structurally. |
| WPF / Web UI | n/a | Add "Balanced" option to topology selector; visible when Experimental on. |
| Tests | Additive | Add `TemplateGeneratorTests` cases for balanced placement determinism + ring-snap output. |

## UI surfaces

- **Web** — extend the topology radio/select in the generation panel with
  "Balanced (experimental)". Visible only when `ExperimentalEnabled` is on.
- **WPF** — `Views/ExperimentalPanel.xaml` is not the right home (it's for
  post-process settings). Add the option to whatever control hosts the
  current topology selector, with the same Experimental visibility gate.

## Open questions resolved

1. Seeded `Random` — thread through `BuildBalancedRandomPositions`. (Confirmed.)
2. Renderer scope — ring-snap in shared library, both hosts. (Confirmed.)
3. Random semantic — "no ring-snap, ever." (Confirmed.)
4. Experimental gate — Balanced ships as a first-class topology choice (not gated on the master Experimental toggle). It's stable, additive, and matches upstream's mainline behaviour; gating would be busywork.

## Behaviour changes (vs pre-port)

- **Structured topologies always use balanced zone ordering.** Before, Default/Chain/HubAndSpoke/SharedWeb used balanced ordering only when the legacy `ExperimentalBalancedZonePlacement` flag was on. Now they always do (matches upstream's default). For users who had the flag off and `MinNeutralZonesBetweenPlayers = 0`, the same seed produces a different zone ordering — players are distributed evenly around the ring instead of grouped.
- **`ExperimentalBalancedZonePlacement` removed from `GeneratorSettings`.** The field stays on `SettingsFile` for back-compat reads; `SettingsMapper.FromFile` migrates `Topology=Random + flag=true` → `Topology=Balanced` on load and drops the flag thereafter.

## Implementation phasing

Single PR is feasible (~10 commits collapse to one feature). Suggested order:

1. Enum + settings additive changes + obsolete marker (compiles, no behavior change).
2. `BuildVariantBalanced` + ring-letter + tournament cluster in generator.
3. Ring-snap pass in renderer, gated.
4. WPF + Web topology selector wiring + Experimental gate.
5. Tests: determinism, ring-snap geometry, settings round-trip.
6. Update memory: experimental rollout note, codebase map.

## References

- Triage detail: `UPSTREAM.md` rows for the 10 commits.
- Experimental pattern: `docs/plans/2026-05-10-experimental-features-rollout.md`.
- Renderer divergence: `docs/plans/2026-05-10-png-preview-rework-design.md`.
