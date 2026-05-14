# Completeness TODOs

Agents pick the next unowned, unblocked task and work it end-to-end. One task per
PR. Update the **Status** and **Owner** lines in this file as part of the PR.

---

## Mindset

The goal is **completeness of template generation**, not feature breadth. A user
opening this tool should be able to express every meaningful .rmg.json template
the game accepts, using every catalog the game ships with.

That means:

1. **Expose what the schema already supports.** Every nullable field on
   `RmgTemplate` / `Zone` / `Connection` / `MainObject` is a knob the game reads.
   If we emit `null`, we are silently narrowing user choice.
2. **Reflect the full game catalog.** If `CommunityData/` has it, the picker
   should let users reach it. Today the SID catalog covers ~20 of hundreds of
   object types; skills and subclasses are loaded but invisible.
3. **Stop at template generation.** This tool emits `.rmg.json`. It is not a
   scenario editor, not a map painter, not a balance simulator. Features that
   require runtime game state are explicit non-goals (see bottom).

Polish, mobile UX, and ergonomics matter only when they block someone from
reaching completeness. Fix the broken mobile layout (T-301) because users on
phones cannot finish; defer drag-drop import because the file picker works.

## Path / sequencing rationale

We tackle in four phases. Each phase unblocks the next.

1. **Schema surface first** (T-001 → T-006). The generator currently emits
   `null` for connection/main-object/zone fields the game uses. Filling these
   in is small per-item but unlocks expressiveness everything else depends on.
   Done first because it has no UI dependency on catalog growth.
2. **Catalog depth** (T-101 → T-104). The biggest visible gap: pickers expose a
   sliver of what's loaded. We mine `CommunityData/` and grow `SidCatalog`,
   then add skills/subclasses pickers, then a refresh pipeline so the snapshot
   doesn't rot.
3. **Generation tuning** (T-201 → T-204). Once knobs and catalogs exist, the
   tuning math (guard distributions, value overrides, per-zone limits) lets
   templates feel hand-authored. These build on phase 1.
4. **UX that supports completeness** (T-301 → T-304). Validation, mobile, and
   per-feature experimental toggles. Strictly the items that gate users from
   exercising the new surface area.

Tasks within a phase can run in parallel unless they declare a `Blocked by:`.

---

## Phase 1 — Expose schema surface

### T-001 — Connection: length, gatePlacement, escape hatch
- **Status:** done
- **Owner:** orchestrator (Phase 1 batch)
- **Effort:** S
- **Files:** `src/OldenEra.Generator/Services/TemplateGenerator.cs`,
  `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs`,
  `src/OldenEra.Generator/Models/Unfrozen/Connection.cs` (no schema change),
  `src/OldenEra.Web/Components/`, `src/OldenEra.TemplateEditor/MainWindow.xaml`.
- **Scope:** Surface `Connection.length`, `gatePlacement`,
  `portalPlacementRulesFrom/To`, `guardEscape`, `simTurnSquad`. Add fields to
  `GeneratorSettings`, wire through both UIs, populate during connection build.
- **Acceptance:**
  - Generated templates with non-default values emit the fields and round-trip
    through `HostParityTests`.
  - At least one preset uses a non-default value to prove the path.
  - Snapshot test for a chain topology with `length` set.

### T-002 — MainObject: guardChance, removeGuardIfHasOwner
- **Status:** done
- **Owner:** Rannes (Phase 1 batch)
- **Effort:** S
- **Files:** `TemplateGenerator.cs` (`BuildNeutralZone` mainObject builder),
  `GeneratorSettings.cs`, both UI hosts.
- **Scope:** Allow neutral castles to be partially-guarded
  (`guardChance < 1.0`) and to drop guards on capture
  (`removeGuardIfHasOwner = true`). Settings already partly model this; emitter
  ignores them.
- **Acceptance:** Settings round-trip; a neutral castle with `guardChance: 0.3`
  appears in fixture output; existing tests still pass.

### T-003 — RmgTemplate.valueOverrides
- **Status:** done
- **Owner:** Rannes (Phase 1 batch)
- **Effort:** S
- **Files:** `Models/Generator/`, `TemplateGenerator.cs`, both UI hosts.
- **Scope:** Add a `List<ValueOverride>` (sid + value or sid + variant + value)
  to `GeneratorSettings`. Emit as `valueOverrides`. Provide a small editor list
  (similar to spell bans) on both hosts.
- **Acceptance:** Empty list → field omitted (preserves clean diffs). Non-empty
  list emits as the game expects. New unit test for emission shape.

### T-004 — Zone.guardReactionDistribution
- **Status:** done
- **Owner:** Rannes (Phase 1 batch)
- **Effort:** M
- **Files:** `TemplateGenerator.cs` (`BuildNeutralZone`, `BuildSpawnZone`),
  `GeneratorSettings.cs` (new TuningSettings field).
- **Scope:** Replace the implicit "spawn day 1" with a configurable weekly
  curve. Default to current behavior so existing snapshots are stable.
- **Acceptance:** Default-settings output is byte-identical to current. New
  setting alters the emitted array. Tests cover both.

### T-005 — Zone.diplomacyModifier, crossroadsPosition, contentBiome
- **Status:** done
- **Owner:** Rannes (Phase 1 batch)
- **Effort:** S
- **Files:** Zone builders in `TemplateGenerator.cs`, settings models, both UI
  hosts.
- **Scope:** Three independent zone-level knobs. Group them so reviewers see
  one cohesive PR. Each defaults to "auto/null", emitting nothing unless set.
- **Acceptance:** Round-trip through `SettingsFile` and `HostParityTests`.

### T-006 — Zone.contentCountLimits, guardCutoffValue, content pool assignments
- **Status:** done
- **Owner:** orchestrator (Phase 2 batch)
- **Blocked by:** T-005 (shares the per-zone settings UI surface)
- **Effort:** M
- **Files:** Zone builders, `GeneratorSettings.cs`, UI hosts, possibly new
  `PerZoneOverridesPanel` component.
- **Scope:** Per-zone caps for SIDs (separate from global), explicit
  `guardCutoffValue`, explicit `guardedContentPool` / `unguardedContentPool`
  selection. Today these are inferred from tier; allow override.
- **Acceptance:** Tournament/balanced-placement snapshots unchanged when
  overrides are absent. Override emits and round-trips.

---

## Phase 2 — Catalog depth

### T-101 — Expand ZoneContentSidCatalog to broad coverage
- **Status:** done
- **Owner:** orchestrator (Phase 2 batch)
- **Effort:** L
- **Files:** `src/OldenEra.Generator/Services/ZoneContent/ZoneContentSidCatalog.cs`,
  `src/OldenEra.Generator/CommunityData/` (read-only), test fixtures.
- **Scope:** The single biggest catalog gap. Today ~20 SIDs. Mine
  `CommunityData/` and any `ExampleTemplates/*.rmg.json` for distinct SID
  strings, group by category (artifacts by tier, creature dwellings by tier,
  learning structures, banks, utopias, resource generators, scholars, war
  machines, footholds, portals, mandatory). Drive the picker from this.
- **Acceptance:**
  - Catalog has every SID found in the example templates (verified by a unit
    test that scans them and asserts coverage).
  - Categories are stable, documented, and ordered.
  - Existing presets still validate.

### T-102 — Skills + subclasses pickers
- **Status:** cancelled (2026-05-14)
- **Owner:** —
- **Effort:** M
- **Cancellation rationale:** Schema verification (full sweep of
  `src/OldenEra.TemplateEditor/GameData/ExampleTemplates/*.rmg.json` plus the
  `GlobalBans` model in `Models/Unfrozen/Miscellaneous.cs`) shows the
  `.rmg.json` contract has **no ban or availability surface for skills or
  subclasses**. `globalBans` accepts only `items`, `heroes`, `magics`. No
  example template uses any skill/subclass-shaped key. `CommunityCatalog`
  loads skills.json / subclasses.json as **descriptive wiki metadata** from
  the alcaras community datamine — these IDs are not template inputs.
  Building this UI would either emit invented keys the game silently ignores
  or persist settings with zero effect on the emitted file. Both violate the
  completeness mindset. If a future patch exposes such a field, reopen — the
  hero/spell ban pattern (`SettingsFile.GlobalBans`, `HeroesPanel`,
  `UnitBanGrid`) is mechanical to extend.

### T-103 — More preset archetypes
- **Status:** done
- **Owner:** orchestrator (Phase 2 batch)
- **Blocked by:** T-101 (depends on broader SID catalog)
- **Effort:** M
- **Files:** `src/OldenEra.Generator/Services/PresetCatalog.cs`,
  `src/OldenEra.Generator/Services/ZoneContent/ZoneContentPresets.cs`.
- **Scope:** Today's 3 top-level presets (Jebus / Arcade 2v2 / Big FFA) cover
  one playstyle each. Add: economy-heavy, magic-focused, aggressive/rush,
  late-game scaling, faction-strategy variants. Aim for ~10 presets total
  spanning 2/4/6/8 player counts.
- **Acceptance:** Each preset generates without validation warnings on default
  settings. Each preset has a one-sentence description visible in the picker.

### T-104 — Community-data refresh workflow
- **Status:** done
- **Owner:** orchestrator (Phase 2 batch)
- **Effort:** M
- **Files:** `src/OldenEra.Generator/CommunityData/scripts/fetch-from-alcaras.py`,
  `.github/workflows/`.
- **Scope:** Document and automate refresh from `alcaras/homm-olden`. Add a
  scheduled GitHub Action (weekly) that runs the script, opens a PR if
  catalogs change, and runs the test suite against the new data.
- **Acceptance:** A dry-run of the workflow succeeds. README updated. Stale
  data is no longer a silent failure mode.

---

## Phase 3 — Generation tuning

### T-201 — Encounter holes (multi-stack battles)
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch)
- **Effort:** M
- **Files:** `TemplateGenerator.cs` (`Zone.encounterHolesSettings`,
  `GameRules`), `GeneratorSettings.cs`, both UI hosts.
- **Scope:** Surface the encounter-holes setting (currently hardcoded false in
  `GameRules`). Add UI toggle and per-zone override.
- **Acceptance:** Toggle round-trips; snapshot diff shows only the intended
  field flipping.

### T-202 — Mandatory content placement rules
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch)
- **Effort:** L
- **Files:** New rules editor in both UI hosts, `MandatoryContent` emission in
  `TemplateGenerator.cs`.
- **Scope:** `ContentItem.rules` lets templates pin mandatory content with
  position/min/max/weight constraints. Enables scenario-style authoring while
  staying inside the .rmg.json contract. Build a small repeatable row editor.
- **Acceptance:** Rules round-trip; an example preset uses one to demonstrate.

### T-203 — MetaObjectsBiome selectors and themed pools
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch)
- **Effort:** S
- **Files:** Zone builders, settings models, both UI hosts.
- **Scope:** Expose `metaObjectsBiome` as a preset selector (e.g.,
  swamp/desert/snow themes). Auto-match remains the default.
- **Acceptance:** Preset selection emits the field; default omits it.

### T-204 — Per-tier-8 neutral creature support in pickers
- **Status:** done (no-op; verified already covered)
- **Owner:** orchestrator (Phase 3+4 batch)
- **Effort:** S
- **Files:** `tests/OldenEra.Generator.Tests/CommunityCatalogTests.cs`
  (regression net only).
- **Scope:** Neutral units reach tier 8 in `units.json` but no preset or
  picker exposes that tier. Add a tier-8 option for guard pools.
- **Resolution (2026-05-14):** Verified no-op. Both unit-ban pickers —
  `src/OldenEra.Web/Components/UnitBanGrid.razor` (Blazor) and
  `src/OldenEra.TemplateEditor/Views/ExperimentalPanel.xaml.cs`
  `PopulateBanUnitPicker` (WPF) — already group `CommunityCatalog.Units`
  by `(Faction, Tier)` dynamically. The four tier-8 neutral entries
  (`avatar`, `avatar_nature`, `avatar_unfrozen`, `lich_dragon`) render as
  `T8. Avatar` etc. with no hardcoded tier ceiling. The `.rmg.json`
  contract has no separate "guard pool tier-N" surface that takes unit
  tiers — `random_hire_*` SIDs and `basic_content_list_..._tier_N`
  content lists are game-side and stop at tier 7 / tier 3 respectively
  (no `random_hire_8` exists in `KnownValues.cs` or in any shipped
  `ExampleTemplates/*.rmg.json`). Inventing a tier-8 hire SID would emit
  a key the game silently ignores — same anti-pattern that cancelled
  T-102. Added a regression test
  (`Units_Tier8_AreNeutralAndReachableViaCatalog`) that pins the
  catalog-side invariant so a future catalog refresh that drops tier-8
  entries fails loudly instead of silently regressing the picker.
- **Acceptance:** A preset using tier-8 neutrals validates and generates.
  → Acceptance criterion is presentation-only and already satisfied:
  tier-8 entries appear in both pickers, are bannable through
  `GlobalBans`, and round-trip through `SettingsFile`. No preset change
  needed because no template field accepts tier-8 unit SIDs as guard
  pool members.

### T-205 — Per-tier terrain density
- **Status:** done
- **Owner:** Rannes
- **Effort:** M
- **Files:** `src/OldenEra.Generator/Services/TemplateGenerator.cs`
  (`ApplyExperimentalSettings`), `src/OldenEra.Web/Components/PerTierOverridesPanel.razor`,
  `src/OldenEra.TemplateEditor/Views/ExperimentalPanel.xaml`,
  `tests/OldenEra.Generator.Tests/`.
- **Scope:** `TierOverrides.ObstaclesFill` / `LakesFill` already exist on
  `GeneratorSettings` and round-trip via `SettingsMapper`, but the generator
  ignores them. Wire them through `ApplyExperimentalSettings` gated on
  `ExperimentalFlags.PerTierOverrides`. Strategy: clone only diverging
  layouts. For each base neutral layout (Sides / TreasureZone / Center),
  resolve effective `(obstacles, lakes)` per tier with precedence
  tier > global Terrain > baseline; mutate the base layout in place when
  all using-tiers agree, otherwise add a suffixed clone (e.g.
  `zone_layout_sides_high`) to `template.ZoneLayouts` and rewrite
  `zone.Layout` for that tier's neutral zones. Surface obstacles/lakes
  sliders per tier in both UI hosts.
- **Acceptance:** Defaults emit byte-identical output. A tier with a
  density override produces one cloned layout per (base layout, tier)
  pair actually used by neutral zones, and retargets those zones'
  `zone.Layout` to the clone. Tiers with no override stay on the base
  layout (which still carries any global `Terrain` stamp). Tier override
  beats global `Terrain` for that tier. Unit tests cover defaults,
  single-tier override, and tier-vs-global precedence.

---

## Phase 4 — UX that supports completeness

### T-301 — Mobile layout: web preview + Generate reachable below 600 px
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch)
- **Effort:** M
- **Files:** `src/OldenEra.Web/wwwroot/css/app.css`,
  `src/OldenEra.Web/Pages/Home.razor`, `src/OldenEra.Web/Components/PreviewPanel.razor`.
- **Scope:** At narrow widths the preview column overflows and Generate is off
  screen. Restack to single column, make preview collapsible, keep the action
  bar pinned. Validate on iPhone-SE-class viewports.
- **Acceptance:** Playwright e2e test at 375×667 reaches Generate and
  downloads a template.

### T-302 — Per-experimental-feature toggles
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch 2)
- **Effort:** M
- **Files:** Both UI hosts, `SettingsFile.cs`, `SettingsMapper.cs`,
  new `Services/ExperimentalFeatures.cs`,
  `Components/ExperimentalFeaturesMenu.razor`,
  `Views/ExperimentalPanel.xaml`.
- **Scope:** `Experimental ⚗` bundled 5 unrelated features at different
  stability levels. Split into per-feature flags (game-mode,
  starting-bonuses, zone-content, borders/roads, per-tier overrides) with a
  registry-driven "graduated" marker so stable ones can promote without
  removing the toggle.
- **Acceptance:** Existing experimental settings auto-migrate. Per-feature
  state round-trips through `.oetgs`.

### T-303 — Inline validation remediation
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch 2)
- **Effort:** M
- **Files:** `SettingsValidator.cs`, both UI hosts.
- **Scope:** Validator already produces actionable messages. Surface them
  inline on the offending control (red border + popover) instead of only in a
  side panel. Add a "fix" affordance where the remediation is mechanical
  (e.g., "Add a neutral zone" → button that does it).
- **Acceptance:** Each blocker has an inline anchor. Manual smoke covers the
  five most common blockers.

### T-304 — Preview: zoom + pan + reseed-in-place
- **Status:** done
- **Owner:** orchestrator (Phase 3+4 batch)
- **Effort:** M
- **Files:** `PreviewPanel.razor`, WPF preview adapter,
  `TemplatePreviewRenderer.cs` (only if vector hooks needed).
- **Scope:** Large maps render unreadable at fixed size. Add zoom/pan controls
  on the PNG. Add a one-click "regenerate with new seed" that re-renders
  without scrolling away from the current view.
- **Acceptance:** Manual: zoom and pan responsive; reseed keeps view position.

---

## Out of scope / non-goals

The tool emits `.rmg.json` and stops there. These are explicit non-goals:

- Live map preview using game assets (renderer stays schematic).
- Balance simulation, AI playthroughs, win-rate prediction.
- Editing scenario maps (`.h5m`-style content).
- A server-hosted catalog or login-based preset sharing. Share-link codec is
  the intended ceiling.
- Localization. English only until the catalog stabilizes.

If a task seems to drift toward these, stop and reopen the discussion.

---

## How to claim a task

1. Pick the lowest-numbered open task with no `Blocked by:` outstanding.
2. Edit this file in the same branch: set **Status: in-progress**, set
   **Owner:** to your handle/agent id, commit alongside your work.
3. On merge: set **Status: done** and link the PR. Do not delete the entry —
   the audit trail is the point.
