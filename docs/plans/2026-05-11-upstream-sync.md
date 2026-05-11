# 2026-05-11 — Upstream sync review

Snapshot of `KhanDevelopsGames/Olden-Era---Template-Generator` reviewed today.

- Merge base: `1bf6fd7`
- Divergence: 141 commits ahead, 21 commits behind
- Newest upstream commit reviewed: `edb64cb`

The full per-commit classification and the sync workflow live in `UPSTREAM.md`. This
file captures the *reasoning* and is the brief for the deferred work; rows there are
the source of truth for the cursor.

## Already-have

The renderer fixes (`49b88d1`, `2ae2c56`) and `GuardChance=1.0` change (`7d99e5b`)
landed in our tree via the earlier `aa5ff5d chore(upstream): port 0.6.x changes`
sync. Verified by reading the equivalent lines in
`src/OldenEra.Generator/Services/TemplatePreviewRenderer.cs` and
`TemplateGenerator.cs`. No action.

## Skipped

| Commit | Why skipped |
| --- | --- |
| `aa88894` (hide preview) | Their motivation was renderer bugs; ours are fixed. |
| `dded712`, `8a077aa` (XAML text/layout tweaks) | Targets the monolithic `MainWindow.xaml` we replaced with per-panel UserControls. Cherry-picking would conflict end-to-end. |
| `364d435` (dropdown fix) | Patches `Themes/MedievalTheme.xaml` which we deleted. Our `ModernDarkTheme` ComboBox is unaffected. Re-evaluate if the symptom shows up. |

## Quick wins (deferred)

`c1106d6` (Discord link) and `2c4f619` (GitHub link) should be ported as a small
self-contained PR adding header buttons in both WPF (`MainWindow` header bar) and Web
(`MainLayout`/`HeaderBar`). The GitHub URL must point at this fork, not upstream. No
brainstorm needed — straightforward.

`6cab0ae` (outdated road hint) is a one-line check: confirm no equivalent stale hint
survived the panel split, remove if so.

## Big initiative — PR #17 customizable starting-zone content

Upstream commits `0c45c42` → `55a31a4` → `f47fae3` → `4a76ce2` → `edb64cb` together
introduce:

- A new `Services/ContentManagement/` namespace abstracting hard-coded SIDs and rules
  out of `TemplateGenerator`. Files: `ContentItemBuilder`, `ContentItemGroup`,
  `ContentPresets`, `DistancePresets`, `RulePresets`, `SidMapping`, `ZoneContentManager`.
- A new `ZoneContentItemUI` view-model and `PlayerZoneMandatoryContent : List<ContentItem>`
  added to `GeneratorSettings` and `SettingsFile` (JSON name
  `playerZoneMandatoryContent`).
- ~800 lines of WPF XAML + ~450 lines of code-behind for the editor UI (DataGrid +
  preset buttons + add/remove/reorder).
- The `TemplateGenerator` switches from emitting hard-coded `MandatoryContent` for
  player zones to reading from this list.

### Why this needs its own brainstorm

- **Two hosts, one feature.** Upstream only has WPF; we must port to Blazor too. The
  data model lives in our shared `OldenEra.Generator` library, but the UX (DataGrid
  on WPF, what equivalent on web?) needs design.
- **Architectural overlap with our work.** We already have `KnownIds`,
  `GameDataCatalog`, `CommunityCatalog`, and a `HeroCatalog` plus picker UIs (spell
  picker, banned-units grid, hero ban list). The `SidMapping` + `ContentPresets`
  upstream introduces overlaps with our catalog layer; we should reconcile rather
  than duplicate.
- **Settings shape and share-link.** Adding a new repeated-object setting affects
  `SettingsMapper`, `SettingsShareCodec` (gzip+base64url URL fragment), and the
  v1-pinned encoding fixture. Needs a forward-compat story.
- **UI architecture.** WPF has been refactored into per-panel UserControls; the
  upstream UI was added to a single MainWindow. We need to decide which panel hosts
  this — `ZonesPanel` is the natural fit, but it is already dense.
- **JSON output parity.** Our reference-template parity test
  (`tests/OldenEra.Generator.Tests/`) currently asserts Jebus Cross only. The upstream
  refactor changes the emitted JSON shape for player zones; we need a snapshot test
  before the refactor lands so we can see the diff.

### Brainstorm scope to open separately

When we pick this up, the brainstorm should answer:

1. Do we adopt upstream's `Services/ContentManagement/` verbatim, or reshape it on top
   of our `GameDataCatalog`/`KnownIds`?
2. What is the Blazor UX? Repeated component rows? A modal picker? Reuse the
   banned-units grid pattern?
3. Where does it live in the WPF panel layout?
4. How do we encode this in `SettingsShareCodec` without breaking existing v1 links?
5. Is there a worthwhile "presets" UX (Khan's UI ships with quick-add buttons) we can
   share between hosts?

Plan file should be `docs/plans/YYYY-MM-DD-customizable-starting-zone-content-design.md`
once the brainstorm runs.
