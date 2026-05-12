# Zone-content Round 4 — UI design

**Date:** 2026-05-14
**Scope:** Web UI only. WPF port deferred.
**Predecessors:** Rounds 1 (library foundation), 2 (emitter + DTO), 3 (persistence + share-codec).

## Goal

Surface the customizable zone-content feature in the Web (Blazor WASM) host. Round 3 wired up persistence and the share codec across both hosts; Round 4 gives users controls to author the zone-content trees on `GeneratorSettings`:

- `PlayerZoneContent` — single `ZoneContentList`.
- `NeutralZoneContent` — three sub-surfaces: `Global` (`ZoneContentList`), `ByTier` (`Dictionary<NeutralZoneTier, ZoneContentList>` keyed by `Poor | Normal | Rich`), `ByZoneLetter` (`Dictionary<string, ZoneContentList>`).
- `ZoneRoadDecorations` — `List<ZoneRoadDecoration>`.

The feature stays gated behind the existing experimental master toggle.

## Decisions log

| # | Question | Decision |
|---|----------|----------|
| Q1 | Web framework approach | New component subtree under `ExperimentalZonePanel`. Outer `ExperimentalCard` chrome, master+detail layout inside. |
| Q2 | Master+detail layout | Tab strip across the editor (Player / Neutral Global / Neutral Poor / Normal / Rich / Per-zone / Road decorations). Each tab is master list + detail pane. Per-zone tab adds a nested zone-letter selector. |
| Q3 | Presets | `ZoneContentPresets` are row-level templates. The item list shows an `+ Add from preset…` button next to `+ Add`; selecting a preset appends a clone of its `Item` to the current scope's list. No confirm modal — additive insertion only. |
| Q4 | Inspect-defaults UX | Toggle re-renders the existing preview against a defaults-blanked settings clone. Editor goes read-only while the toggle is on. |
| Q5 | WPF vs Web sequencing | Web first. WPF port is a separate follow-up round. |

## Architecture

### Components (new, all under `src/OldenEra.Web/Components/ZoneContent/`)

- `ZoneContentEditor.razor` — top-level coordinator. Owns selection state and the defaults-compare toggle. Renders defaults-compare toggle, scope tabs, and the active scope's editor surface. Embedded inside `ExperimentalZonePanel.razor` as a single `ExperimentalCard` (`Key="zone-content"`, `Title="Zone content"`).
- `ZoneContentScopeTabs.razor` — tab strip. Player, Neutral Global, Neutral Poor, Neutral Normal, Neutral Rich, Per-zone, Road decorations.
- `ZoneContentItemList.razor` — master list for a scope's `List<ZoneContentItem>`. `+ Add`, `+ Add from preset…`, and `Remove` buttons. No reorder in v1 (order is not semantic).
- `ZoneContentItemDetail.razor` — single-item editor. Sid picker, Handle, MinCount, MaxCount, Pool, IsGuarded, NearCastle, RoadDistance, FactionAffinity, BiomeFilter.
- `ZoneRoadDecorationsEditor.razor` — list+detail in one component. Fields per row: zone letter, RoadType, From and To endpoint kind+arg.
- `PerZoneOverridesPicker.razor` — nested zone-letter selector, used only inside the Per-zone tab. Free-text letter input plus a list of letters that already have overrides.
- `ZoneContentWarningBadge.razor` — inline pill that renders one validator warning next to the offending knob.

### State

`ZoneContentEditor` holds the editor's transient selection state. Nothing here lives on `GeneratorSettings`.

- `SelectedScope` — enum: `Player | NeutralGlobal | NeutralPoor | NeutralNormal | NeutralRich | NeutralPerZone | RoadDecorations`.
- `SelectedZoneLetter` — string. Active only when `SelectedScope == NeutralPerZone`.
- `SelectedItemHandle` — string?. Identifies the open item in the detail pane. Falls back to list index when Handle is empty or duplicate.
- `CompareDefaultsOn` — bool. While true, the preview renders defaults and the editor goes read-only.

### Mutation rule

Round 3's `SettingsMapper` aliases the zone-content trees rather than deep-copying. The comment at `SettingsMapper.cs:160` is load-bearing: the UI must clone before mutating list shape.

- **List-shape change** (add, remove, preset insert) — clone the affected `ZoneContentList` (or the dictionary entry) before mutating, then assign back. A new `ZoneContentCloning` helper centralizes this.
- **Scalar field edits** (MinCount, IsGuarded, etc.) — direct mutation. The alias concern is about list-instance identity at the mapper boundary, not field writes.
- **Defaults-compare path** — clones for its own throwaway settings; never mutates the user's tree.

### Component contracts

```
ZoneContentEditor
  [Parameter] GeneratorSettings Settings
  [Parameter] EventCallback OnChanged
  // owns: SelectedScope, SelectedZoneLetter, SelectedItemHandle, CompareDefaultsOn

ZoneContentScopeTabs
  [Parameter] ZoneContentScope Selected
  [Parameter] EventCallback<ZoneContentScope> SelectedChanged

ZoneContentItemList
  [Parameter] List<ZoneContentItem> Items
  [Parameter] string? SelectedHandle
  [Parameter] EventCallback<string?> SelectedHandleChanged
  [Parameter] EventCallback OnItemsChanged
  [Parameter] IReadOnlyDictionary<string, IReadOnlyList<EmitWarning>> WarningsByHandle
  [Parameter] bool ReadOnly

ZoneContentItemDetail
  [Parameter] ZoneContentItem? Item
  [Parameter] EventCallback OnChanged
  [Parameter] IReadOnlyList<EmitWarning> Warnings
  [Parameter] bool ReadOnly

ZoneRoadDecorationsEditor
  [Parameter] List<ZoneRoadDecoration> Decorations
  [Parameter] EventCallback OnChanged
  [Parameter] bool ReadOnly
```

## Defaults-compare toggle

When `CompareDefaultsOn` flips on:

1. The editor signals the page to render the preview against a clone produced by `ZoneContentCloning.CloneWithDefaultsBlanked(settings)` — a copy with `PlayerZoneContent`, `NeutralZoneContent`, and `ZoneRoadDecorations` reset to fresh empty instances.
2. Master and detail panes set `ReadOnly=true`. Inputs disable; Add, Remove, and Add-from-preset disable. A banner reads "Showing defaults — editing paused."
3. Toggling off restores normal rendering. The user's edits are preserved on `Settings`.

## Preset insertion (row-level)

`ZoneContentPresets.All()` returns row-level `ZoneContentPreset(Name, Item)` entries (`Mana Well x1 (guarded)`, `Pandora Army x1 (guarded, near castle)`, etc.). The item list surfaces an `+ Add from preset…` dropdown:

1. User opens the dropdown, picks a preset.
2. The editor clones `preset.Item` and appends it to the current scope's list.
3. Selection moves to the new item so the user can immediately tweak Handle / counts / etc.

No confirm modal — preset insertion is additive and reversible by remove.

## Warnings

The generator already exposes `ZoneContentEmitWarnings.Inspect(item, zoneName)` returning `IReadOnlyList<EmitWarning>` (kinds: `BiomeFilterIgnored`, `FactionAffinityIgnored`, `PoolNonMandatoryDropped`, `MinCountRangeNarrowedToMax`). Round 4 reuses this directly — no new validation service needed.

A small `ZoneContentWarningProjection` helper in the web project iterates the four trees, calls `Inspect` per item, and produces `IReadOnlyDictionary<(scope, handle), IReadOnlyList<EmitWarning>>`. The editor slices by scope+handle and passes warnings to list and detail.

- Detail pane: a `ZoneContentWarningBadge` next to each flagged control. Mapping warning code → control:
  - `BiomeFilterIgnored` → BiomeFilter row.
  - `FactionAffinityIgnored` → FactionAffinity row.
  - `PoolNonMandatoryDropped` → Pool dropdown.
  - `MinCountRangeNarrowedToMax` → MinCount/MaxCount row.
- List rows: a single aggregated badge ("⚠ 2") with the kinds in its tooltip.

## Testing

- **Unit (web).** `ZoneContentCloning` tests confirm the clone produces independent list instances and that mutating the clone leaves the source intact.
- **Unit (web).** `ZoneContentWarningProjection` tests feed crafted `GeneratorSettings` and assert the projected dictionary maps the right `(scope, handle)` tuples to the expected `EmitWarning` codes. The generator-side validation tests cover the underlying logic; the projection test only covers the scope+handle mapping.
- **Component (bUnit, optional).** Tab switching, list add and remove, preset insertion, defaults-compare toggle disabling inputs. Skip if bUnit is not already in the repo.
- **Manual browser verification.** `dotnet watch run --project src/OldenEra.Web/OldenEra.Web.csproj`. Exercise: experimental toggle → zone-content card → each tab edits and persists across tab switches → preset insertion → defaults-compare toggle → warnings render next to controls → preview reflects edits. DevTools console is the source of truth for Blazor errors.
- **No new generator tests.** Round 4 is UI-only; the generator round-trip is covered by Rounds 1–3.

## Commit slicing

Target ≈7 commits, TDD where it pays.

1. `ZoneContentCloning` helper + tests.
2. `ZoneContentWarningProjection` + tests.
3. `ZoneContentEditor` shell + `ScopeTabs` + selection state. No edits yet.
4. `ZoneContentItemList` + `ZoneContentItemDetail` read/write.
5. `ZoneRoadDecorationsEditor`.
6. Preset insertion dropdown + defaults-compare toggle.
7. Warning badges wired into list and detail. Final manual-verification pass.

## Out of scope

- New schema features. The Round 1 schema constraints still hold; biome and faction filters drop with warnings.
- Non-Mandatory pool authoring. v1 still writes Mandatory only.
- New presets. The existing `ZoneContentPresets` set is the v1 surface.
- WPF port. Tracked as a follow-up round.
- Side-by-side preview panes (Q4 alternative B). Revisit if users ask.
- Item reorder UI.

## Round 3 follow-ups

Carry forward, touch only if Round 4 work crosses them:

- Reflection guard `IsDefault` treats `int == 0` as default. `MinCount` defaults to 1, so a zeroed regression would fail with a misleading "populate one" message. Fix only if Round 4 changes the fixture or guard.
- `SettingsFileJsonOptions` factory: extract on the 6th near-identical block. Round 4 does not add one.
