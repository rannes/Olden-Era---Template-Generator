# Zone Content — Round 5 Design

**Date:** 2026-05-13
**Status:** Validated, ready for implementation
**Predecessors:** Rounds 1–4 (PRs #11, #13, #14, #15 — all merged)

Round 5 closes out the customizable zone-content feature. Two threads.

## Goals

1. **Thread B — Sid catalog autocomplete + helper relocation (lands first, on `main`).**
   Wire `ZoneContentSidCatalog` into the Web Sid input as a `<datalist>`. Relocate
   the three host-shared helpers (`ZoneContentCloning`, `ZoneContentScope`,
   `ZoneContentWarningProjection`) from `OldenEra.Web` into `OldenEra.Generator`
   so both hosts can consume them. Add a `Category` field to `ZoneContentPreset`
   so both hosts can group the picker (the picker is designed to scale to ~20
   entries even though we ship with the current four).

2. **Thread A — WPF port (worktree, branch `feature/zone-content-round-5`).**
   Port the Round 4 Web zone-content editor to `OldenEra.TemplateEditor`.
   View-model wrappers per `ZoneContentItem`, tabbed master+detail layout,
   defaults-compare toggle, validator badges, preset picker grouped by category,
   Sid combobox bound to the catalog.

## Out of scope (deferred follow-ups)

- Preset library expansion to ~20 entries (designed-for, not authored this round).
- `ZoneContentSidCatalog` expansion via friendly-name source.
- Schema-gated items: non-Mandatory pool authoring, biome/faction emission,
  pool-by-value.
- Item reorder UI, side-by-side preview panes.
- Round 4 forward-looking items (deep-clone nested mutables in
  `CloneWithDefaultsBlanked`, `#N` selection-key collision) — touch only if
  Round 5 work crosses them.

## Decisions log

- **(a) Thread sequencing — B first, then A.** Web-only catalog work lands
  cheaply on main; the helper relocation is a natural checkpoint; the WPF port
  inherits the catalog so the Sid combobox ships with autocomplete on the first
  pass instead of needing a second WPF CI round-trip.
- **(b) Helper relocation — into `OldenEra.Generator/Services/ZoneContent/`.**
  Both hosts already reference `OldenEra.Generator`; co-locates with
  `ZoneContentSidCatalog`, `ZoneContentPresets`, `ZoneContentItemValidator`. No
  new project file. Drift test moves to `OldenEra.Generator.Tests`.
- **(c) WPF binding — view-model wrappers per `ZoneContentItem`.** Library DTOs
  stay clean (no INPC pollution); WPF-specific concerns (CSV editing, defaults-
  compare lock, badge state) live in the host. Standard WPF pattern.
- **(d) Sid catalog expansion — defer.** Ship the autocomplete with the existing
  4-entry seed; the UX win comes from having a datalist at all. Update the
  catalog comment to clarify expansion is a future round, not "soon."
- **(e) WPF preset picker — per-row grouped `ComboBox`.** Mirrors the Web
  affordance, scales to 20+ via `CollectionViewSource` grouping by `Category`.
- **(extra) Preset count constraint — design for ~20+, ship current four.**
  Both hosts add category-aware grouping (`<optgroup>` on Web, `GroupStyle` on
  WPF) so future preset additions are pure data work.
- **(extra) WPF VM tests — new `OldenEra.TemplateEditor.Tests` project.** VMs
  are pure C# with no WPF assembly dependency, so they build and run on Mac —
  the only Round 5 verification lever short of CI.

## Architecture

### Thread B (on main)

#### Helper relocation

Move three files from `src/OldenEra.Web/Services/` to
`src/OldenEra.Generator/Services/ZoneContent/`:

- `ZoneContentCloning.cs`
- `ZoneContentScope.cs`
- `ZoneContentWarningProjection.cs`

Namespace changes from `OldenEra.Web.Services` to
`OldenEra.Generator.Services.ZoneContent`. Update the 11 referenced
`OldenEra.Web.Tests` files' `using` directives. Update the eight Razor
components in `src/OldenEra.Web/Components/ZoneContent/` for the namespace
change.

Move `tests/OldenEra.Web.Tests/Services/ZoneContentCloningTests.cs` (the
reflection drift test) to `tests/OldenEra.Generator.Tests/Services/ZoneContent/`
and update its namespace. The reflection target (`Settings`, `ZoneContentItem`)
is unchanged; only the test's own namespace moves.

#### `ZoneContentPreset.Category`

```csharp
public sealed record ZoneContentPreset(string Name, string Category, ZoneContentItem Item);
```

Backfill the four existing entries with `Category = "Mandatory"`. New unit test
asserting all entries have non-empty `Category`. No host wiring yet — the field
exists, future preset additions and host grouping will use it.

#### Web Sid `<datalist>`

In the Razor component that owns the Sid `<input>`, render once per editor:

```razor
<datalist id="zone-content-sids">
  @foreach (var entry in ZoneContentSidCatalog.All())
  {
    <option value="@entry.Sid" label="@entry.FriendlyName"></option>
  }
</datalist>
```

Set `list="zone-content-sids"` on the Sid input.

Update `ZoneContentSidCatalog`'s comment to clarify expansion is deferred to a
future round (not "a follow-up will union…").

#### Web preset picker `<optgroup>`

Group `ZoneContentPresets.All()` by `Category` and emit `<optgroup>` wrappers.
With four entries the visual change is null, but the markup is in place for
future preset growth.

### Thread A (worktree)

#### Project layout

```
src/OldenEra.TemplateEditor/
  Views/
    ZoneContentPanel.xaml(.cs)
    ZoneContentScopeTab.xaml(.cs)
    ZoneContentItemRow.xaml(.cs)
    ZoneContentPerZoneSelector.xaml(.cs)
  ViewModels/
    ZoneContentPanelViewModel.cs
    ZoneContentScopeViewModel.cs
    ZoneContentItemViewModel.cs
  Converters/
    ZoneContentSeverityToBrushConverter.cs

tests/OldenEra.TemplateEditor.Tests/
  ViewModels/
    ZoneContentItemViewModelTests.cs
    ZoneContentScopeViewModelTests.cs
    ZoneContentPanelViewModelTests.cs
```

The new test project references only the VM source files and
`OldenEra.Generator`, **not** any WPF assemblies, so it builds on Mac.

#### View-model boundaries

`ZoneContentItemViewModel` is the only INPC-bearing wrapper. Exposes typed
surfaces for every `ZoneContentItem` field, plus `FactionAffinityCsv` and
`BiomeFilterCsv` strings that round-trip to `List<string>`. `ToModel()` produces
a fresh `ZoneContentItem`; `FromModel(item)` rehydrates after defaults-compare
toggle.

`ZoneContentScopeViewModel` exposes `ObservableCollection<ZoneContentItemViewModel>
Items`, the scope label, and a nullable per-zone letter (only Per-zone scope
uses it).

`ZoneContentPanelViewModel` owns `Settings`, exposes the seven scope VMs,
exposes `IsDefaultsCompareActive` and `IsReadOnly`. Toggling defaults-compare
swaps `Settings` → `ZoneContentCloning.CloneWithDefaultsBlanked(Settings)` and
sets `IsReadOnly = true`. Raises `Changed` after every commit so `MainWindow`
can rebuild the preview.

#### UI specifics

- `ZoneContentPanel` is a `UserControl` with a `DockPanel`: top-docked toolbar
  (defaults-compare `ToggleButton`, aggregated warning count), body
  `TabControl` with seven tabs.
- Tab headers carry a small badge `Border` showing worst-severity warning count
  for that scope.
- Per-zone tab is a horizontal split: `ListBox` of letters on the left, the
  `ZoneContentScopeTab` for the selected letter on the right.
- `ZoneContentScopeTab` renders `Items` via `ItemsControl` with a
  `StackPanel` panel and a `ZoneContentItemRow` `DataTemplate`. Below it: a
  preset `ComboBox` styled "+ Add from preset…" with `CollectionViewSource`
  grouping by `Category`. `GroupStyle` shows a bold category header.
- `ZoneContentItemRow` is a `Grid`: Sid combobox (`IsEditable=true`,
  `IsTextSearchEnabled=true`, items from `ZoneContentSidCatalog`), Min/Max
  numeric `TextBox`es, Pool `ComboBox`, three checkboxes, two CSV `TextBox`es,
  delete button, aggregated warning badge. Per-field warning badges next to
  each field with tooltip messages.
- All editable controls bind `IsEnabled` to `!PanelViewModel.IsReadOnly` via
  `RelativeSource` and an inverse-bool converter.

**Style ordering note:** every `<Style>` in the XAML keeps `Setter`s before
`Triggers` (memory: `feedback_wpf_style_child_order`). An inline XML comment
near each Style block flags this, since BAML errors only surface in CI on Mac.

## Risks

1. **CSV round-trip edge cases** in `FactionAffinityCsv`/`BiomeFilterCsv` —
   whitespace, empty strings, trailing commas. Mitigated by VM unit tests.
2. **Defaults-compare leak** — if any VM captures `Settings.ZoneCfg` directly
   instead of going through the panel, the toggle leaks edits to the original.
   Mitigated by VMs holding only their item slice.
3. **`CollectionViewSource` refresh** on collection mutation — known WPF
   papercut. Preset list is static `IReadOnlyList`, so not currently a problem.
4. **`Style.Triggers` ordering** — covered by memory and inline XML comments.
5. **Reflection drift test relocation** — types unchanged, namespace-only move.

## Success criteria

- Thread B PR merged: helpers relocated, `Category` present, Web datalist +
  optgroups working, manual browser verification done, all existing tests green.
- Thread A PR merged: WPF panel functional, CI green across all commits, VM
  unit tests pass on Mac, fresh-eyes reviewer pass clean.
- Both hosts share `ZoneContentSidCatalog`, `ZoneContentPresets`,
  `ZoneContentCloning`, `ZoneContentScope`, `ZoneContentWarningProjection`
  from `OldenEra.Generator`.
- Sid input has friendly-name autocomplete on both hosts.
- Preset picker grouped by category on both hosts.
- Defaults-compare toggle works on both hosts.
- No regressions in emitter output (existing emitter tests still green).

## Workflow

1. Brainstorm (this doc) — done.
2. `superpowers:writing-plans` →
   `docs/plans/2026-05-13-zone-content-round-5-impl.md`.
3. Commit design + impl plan to `main`.
4. Execute Thread B on `main`, small commits, PR against fork, merge.
5. `superpowers:using-git-worktrees` → `.worktrees/zone-content-round-5`,
   branch `feature/zone-content-round-5`, off `main` after Thread B merges.
6. Execute Thread A via `superpowers:subagent-driven-development`. After Task 1,
   default to dispatching unattended; check in only on blockers.
7. Each WPF task: small commit, push, watch CI. There is no Mac dev-server
   equivalent; CI is the verifier.
8. `superpowers:requesting-code-review` fresh-eyes pass before merge.
9. PR via `gh pr create --repo rannes/Olden-Era---Template-Generator`.
