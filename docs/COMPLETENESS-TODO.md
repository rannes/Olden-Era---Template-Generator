# Completeness TODOs

Agents pick the next unowned, unblocked task and work it end-to-end. One task per
PR. Update the **Status** and **Owner** lines in this file as part of the PR.

Last full rescan + verification pass: 2026-05-15. Each Phase 5–8 entry was
checked against the codebase before being filed. See [Done log](#done-log)
for the prior phases (T-001 → T-304).

Phase 5–8 rescan summary:
- Confirmed already shipped (closed before any work): **T-507** border noise
  + variant orientation jitter; **T-510** spell bans by school.
- Partially shipped (need narrowing rather than full build): **T-503**,
  **T-504**, **T-506**, **T-801**. Scope sections now spell out exactly what
  remains.
- **Phase 5 complete (2026-05-15):** T-501 → T-510 all done. Remaining open:
  Phase 6 (T-601 → T-606); Phase 7 (T-701 → T-705); T-802 → T-808.

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
   should let users reach it.
3. **Stop at template generation.** This tool emits `.rmg.json`. It is not a
   scenario editor, not a map painter, not a balance simulator. Features that
   require runtime game state are explicit non-goals (see bottom).

A field/feature only earns a task when:
- the schema accepts it, **and**
- a shipped example template or community request actually uses it, **and**
- emitting it under user control changes the resulting map.

If two of three are missing, the work is scenario authoring, not generation.

---

## Path / sequencing rationale

Phases 5–8 are the new round. Each unblocks the next.

5. **Schema gap closure** (T-501 → T-510). Quick wins where the model has the
   field but generator never emits it, plus the small set of model holes the
   shipped example templates exercise. Single PR each, no catalog dependency.
6. **Catalog enrichment** (T-601 → T-606). The community datamine has stat-level
   detail we drop on the floor. Loading it lets every picker grow tooltips
   without rewriting any picker. Also closes the visible `includeLists` gap.
7. **Analysis features** (T-701 → T-705). Read-only computations over the
   generated template. They make balance issues visible *before* the user
   uploads the .rmg.json into a game. Strictly inside the no-runtime-state
   constraint — value math, graph stats, fairness diff — never simulation.
8. **Editor ergonomics + reach** (T-801 → T-808). Undo, search, presets, and
   the parity gaps the upstream issue tracker flags. Each one removes a
   reported friction; none invent new schema.

Tasks within a phase can run in parallel unless they declare a `Blocked by:`.

---

## Phase 5 — Schema gap closure

### T-501 — Connection.guardRandomization (model + emit)
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** S
- **Files:** `src/OldenEra.Generator/Models/Unfrozen/Connection.cs`,
  `TemplateGenerator.cs`, `GeneratorSettings.cs`, both UI hosts.
- **Scope:** `Connection.guardRandomization` is in shipped templates
  (e.g. `All Around.rmg.json:1076`, `0.10`–`0.15`) but our `Connection` model
  has no such property. Add the field, surface a per-template default plus a
  per-connection override.
- **Acceptance:** Round-trip on `All Around.rmg.json` matches input. Default
  null → field omitted.

### T-502 — Zone.guardMultiplier and guardRandomization per-zone overrides
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** S
- **Files:** `Zone.cs` (already modeled), `TemplateGenerator.cs:1150-1158,
  2510-2511, 2870-2871, 2944-2945`, `GeneratorSettings.cs`, per-zone
  overrides panel, both UI hosts.
- **Scope:** Properties exist on Zone and the generator emits them per-zone
  from hardcoded tuning profiles. The only user-facing knob is the global
  `Settings.ZoneCfg.Advanced.GuardRandomization` slider
  (`AdvancedPanel.razor:24`); `EffectiveGuardRandomization` reads only the
  global value. Add per-zone overrides via the panel introduced in T-006.
- **Acceptance:** Defaults byte-identical. Per-zone override emits and
  round-trips.

### T-503 — Per-area content/resource value overrides
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** M
- **Files:** Zone builders (`TemplateGenerator.cs:2520-2525, 2880-2885,
  2954-2959`), `GeneratorSettings.cs`, UI hosts.
- **Scope:** Both scalar and per-area variants are declared on Zone and
  always emitted side-by-side from hardcoded tuning profiles. No
  GeneratorSettings field, no UI for users to override either number.
  Add per-zone overrides for each of `resourcesValue[PerArea]`,
  `guardedContentValue[PerArea]`, `unguardedContentValue[PerArea]`.
- **Acceptance:** Override round-trips, emission reflects user value,
  defaults unchanged.

### T-504 — User-editable RmgTemplate.description / displayWinCondition
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** S
- **Files:** `RmgTemplate.cs:14-18`, `TemplateGenerator.cs:70-71` +
  `BuildTemplateDescription` at 746-779, `GeneratorSettings.cs`, UI hosts.
- **Scope:** Both fields are emitted today: `Description` is auto-built
  from settings; `DisplayWinCondition` derives from
  `effectiveVictoryCondition`. Neither is user-editable. Add a textarea
  for `Description` (override) and a text input for `DisplayWinCondition`;
  empty → fall back to current auto-generated value.
- **Acceptance:** User-supplied text emits verbatim. Empty input keeps
  current auto-generated output (default snapshot byte-identical).

### T-505 — GameRules.holdCityWinCon (top-level)
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** S
- **Files:** `Models/Unfrozen/GameRules.cs`, `TemplateGenerator.cs`.
- **Scope:** Shipped hold-city templates set `gameRules.holdCityWinCon: true`
  in addition to the per-MainObject `holdCityWinCon` flag. Our `GameRules`
  model is missing the property entirely. Add it; have the city-hold game
  mode flip both.
- **Acceptance:** Hold-city preset emits the top-level flag and round-trips.

### T-506 — User-controlled heroLighting + heroLightingDay
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** S
- **Files:** `TemplateGenerator.cs:1047-1048` (inside
  `BuildAdvancedWinConditions` → `WinConditions`), `GeneratorSettings.cs`,
  UI hosts.
- **Scope:** `HeroLighting=true, HeroLightingDay=1` are emitted
  unconditionally on `WinConditions` (verifier confirmed default-on, no
  longer tournament-gated). Expose checkbox + day-number input for
  explicit override / off.
- **Acceptance:** Default snapshot byte-identical. Explicit off omits both;
  custom day round-trips.

### T-507 — Border noise + variant orientation jitter
- **Status:** done (verified 2026-05-15)
- **Owner:** —
- **Effort:** S
- **Resolution:** `Border.ObstaclesNoise`/`WaterNoise` declared
  (`Variant.cs:50-57`) and emitted (`TemplateGenerator.cs:2817, 2819`).
  `Variant` orientation fields `BaseAngleMin/Max`,
  `RandomAngleAmplitude/Step` declared (`Variant.cs:29-39`) and emitted
  (`TemplateGenerator.cs:2808-2811`). No work needed.

### T-508 — Zone randomHire weekly/initial unit increment
- **Status:** done (#56)
- **Owner:** Claude
- **Effort:** S
- **Files:** `Zone.cs` (added `RandomHireEnableWeeklyUnitIncrement` /
  `RandomHireInitialUnitIncrement`, both `List<bool/int>?` per-difficulty
  arrays, JsonIgnore-when-null); `GeneratorSettings.cs` →
  `ZoneOverridesSettings` (empty list = unset, mirroring T-006/T-503
  list-overrides); `SettingsFile.cs` (CSV strings for share-codec safety);
  `SettingsMapper.cs` (CSV codecs both directions, malformed → empty);
  `TemplateGenerator.ApplyZoneOverrides` (clone-per-zone stamp); WPF
  `ExperimentalPanel.xaml` + `MainWindow.xaml.cs`; Web
  `ExperimentalZonePanel.razor` (text inputs + Auto clear).
- **Scope:** Templates in the Arcade/Junction/Universe/All Around/Infinity
  family use `randomHireEnableWeeklyUnitIncrement` (bool[7]) +
  `randomHireInitialUnitIncrement` (int[7]) per Zone to tune random-hire
  creature growth.
- **Acceptance:** Verified by `ZoneRandomHireOverridesTests`: round-trip
  parses Arcade.rmg.json with both 7-entry arrays preserved exactly; default
  emission omits both fields and stays byte-identical across every preset
  in the catalog; mapper + share-codec round-trip; malformed CSV → empty.

### T-509 — MainObject schema completion (owner, isKeyObject, unit-increment, factions list)
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** M
- **Resolution:** Added five nullable round-trip fields to `MainObject`
  (`Owner`, `IsKeyObject`, `EnableWeeklyUnitIncrement`, `InitialUnitIncrement`,
  `Factions`) so shipped templates (Harmony, Shamrock, Hallway, Christmas
  Tree, Symphony, …) survive load → save without losing data. Generator
  never emits these under default settings, keeping every preset
  byte-identical. No new authoring UI — scenario-authoring stays a non-goal.
  See `tests/OldenEra.Generator.Tests/MainObjectSchemaCompletionTests.cs`
  for round-trip + byte-identical sweep coverage.

### T-510 — Spell bans by school (globalBans.magics)
- **Status:** done (verified 2026-05-15)
- **Owner:** —
- **Effort:** S
- **Resolution:** Generator emits `globalBans.magics` from `BannedSpells`
  (`TemplateGenerator.cs:167-176`). Web UI is school-tabbed
  (`HeroSettingsPanel.razor:79-106`, iterates `Catalog.SpellSchools`).
  Round-trip via `SettingsFile.BannedSpells` (`SettingsFile.cs:82`),
  `SettingsMapper.cs:210, 345`. WPF host wires through
  `MainWindow.xaml.cs:909, 1199, 1726`. Closes upstream issue #24.

---

## Phase 6 — Catalog enrichment

### T-601 — Load skill-columns.json
- **Status:** done (PR #58)
- **Owner:** —
- **Effort:** S
- **Files:** `Services/CommunityCatalog.cs`, `CommunityData/skill-columns.json`.
- **Scope:** File is shipped but never loaded. Add a `SkillColumns` collection
  on `CommunityCatalog`. Closes a silent drop. Even if not user-visible yet,
  it unblocks T-602/T-603 tooltips that group skills by column.

### T-602 — Enrich UnitEntry with combat stats
- **Status:** done (PR pending)
- **Owner:** —
- **Effort:** M
- **Files:** `CommunityCatalog.cs` (`UnitEntry` record), `UnitBanGrid.razor`,
  WPF unit picker XAML.
- **Scope:** `units.json` has `attack, hp, off, def, dmgMin, dmgMax, init,
  speed, squadValue, cost, ai, tags, narrative, passives[], abilities[]` —
  every gameplay stat. We load only id/name/faction/tier/variant. Load the
  rest, surface in tooltip on hover (reuses tooltip infra from T-803 if it
  lands first — but does not block).
- **Acceptance:** Tooltip on a unit chip shows tier-correct stats. No
  performance regression in picker open time.

### T-603 — Enrich HeroEntry / SpellEntry / SkillEntry
- **Status:** done (PR pending)
- **Owner:** —
- **Effort:** M
- **Files:** `CommunityCatalog.cs`, picker components in both hosts.
- **Scope:** Single PR loading the rest of the silently-dropped fields:
  - HeroEntry: `specId`, `armyScore`, `stats {A,D,P,K}`, `skills[]`, `army`.
  - SpellEntry: `manaCost[]`, `cooldown`, `learnCost[]`, `icon`, `magicType`.
  - SkillEntry: `baseDesc`, `levels[]`, `subclasses[]`, `starters[]`.
- **Acceptance:** Catalog round-trips, tooltips render at least one new field
  per type, regression tests pin presence.

### T-604 — Fetch alcaras catalog/out/{classes,specializations}.json
- **Status:** done (PR #TBD — t-604-alcaras-classes-specializations)
- **Owner:** —
- **Effort:** M
- **Files:** `CommunityData/scripts/fetch-from-alcaras.py`,
  `CommunityCatalog.cs`, `.github/workflows/` (the T-104 refresh workflow).
- **Scope:** Upstream `alcaras/homm-olden` publishes `catalog/out/classes.json`
  (12 hero classes with stat-roll tables) and `catalog/out/specializations.json`
  (126 specializations keyed by specId). Currently only the `docs/*.js`
  bundles are fetched. Add the two raw JSON files; expose as
  `CommunityCatalog.Classes` / `Specializations`. Pairs with T-603's
  `HeroEntry.specId`.
- **Acceptance:** Fetch script pulls both. Catalog tests lock counts. Existing
  refresh workflow still passes.

### T-605 — ContentList catalog + picker
- **Status:** done (PR pending)
- **Owner:** —
- **Effort:** L
- **Files:** New `Services/ZoneContent/ContentListCatalog.cs`,
  `Components/ContentListPicker.razor`, WPF parity, zone builder wiring.
- **Scope:** Shipped templates reference ~30 distinct `includeLists` IDs
  (e.g. `basic_content_list_building_guarded_resource_banks_tier_3`,
  `basic_content_list_pickup_pandora_box_units`,
  `content_list_building_random_hires_high_tier`) that the SID picker doesn't
  cover. These are first-class zone-content references. Mine the example
  templates and `GameData`/`GeneratorData` for canonical IDs, group by
  semantic category (resource-banks / pickups / random-hires / pandora /
  artifact-tiers), surface a picker.
- **Acceptance:** Coverage test: every `includeLists` ID in any shipped
  template appears in the catalog. Round-trip on Anarchy/AnarchySmall keeps
  the references intact.

### T-606 — Code-gen formulaic SID catalog entries
- **Status:** done (PR #59)
- **Owner:** —
- **Effort:** S
- **Files:** `ZoneContentSidCatalog.cs`.
- **Scope:** ~25 of 106 catalog entries are formulaic (`random_hire_1..7`,
  `mine_<resource>`, `name_mine_<resource>[_N]`, `name_remote_foothold[_N|_NN]`).
  Replace hand-listing with `Enumerable.Range` generation. Reduces drift risk
  when a future patch grows tier ceilings or resource enums.
- **Acceptance:** Catalog snapshot test stays byte-identical (or has the
  intentional new entries explicitly listed). Public API unchanged.

---

## Phase 7 — Analysis features

### T-701 — Zone value budget summary
- **Status:** done (PR #64)
- **Owner:** Claude
- **Effort:** M
- **Files:** New `Components/ValueBudgetPanel.razor`, WPF parity,
  `Services/TemplateAnalysis.cs` (new).
- **Scope:** Read `Zone.ResourcesValue/PerArea`,
  `GuardedContentValue/PerArea`, `UnguardedContentValue/PerArea` off the
  generated `RmgTemplate`; render per-zone and total cards under the preview.
  Pure display over generator output — no simulation.
- **Acceptance:** Summary updates on every regenerate. Numbers match the
  emitted JSON. Hidden when generation has not run.

### T-702 — Guard-power vs. zone-value chart
- **Status:** done (PR pending)
- **Owner:** Claude
- **Effort:** M
- **Files:** `Services/TemplateAnalysis.GuardChart.cs` (new sibling partial),
  `Components/GuardValueChartPanel.razor` (+ `.razor.css`), WPF
  `Views/GuardValueChartPanel.xaml` (+ `.xaml.cs`), wired into `Home.razor`
  and `MainWindow.xaml`/`.xaml.cs` next to the T-701 panel.
- **Scope:** The most common balance bug is "rich zone with weak guards".
  Plot per-zone effective `Zone.GuardMultiplier` (already scaled by
  `BorderGuardStrengthPercent` at emission time) against
  `Zone.ResourcesValue`; flag outliers in red. Inline SVG (Web) / WPF
  Canvas with simple shapes — no chart library.
- **Resolution:** Outlier rule (zone in top quartile of value AND bottom
  quartile of guard, refusing to flag with fewer than 4 plottable points)
  is unit-tested in `TemplateAnalysisGuardChartTests`. Panel hidden until
  generation has run (matches T-701 pattern). Both hosts render the same
  data with identical hover tooltips.

### T-703 — Content-pool sanity warnings (validator extension)
- **Status:** done (#68)
- **Owner:** Claude
- **Effort:** S
- **Files:** `Services/SettingsValidator.cs`, validator UI surface.
- **Scope:** Sweep enabled `ContentLists` and warn when expectations
  obviously fail: "no tier-7 dwellings reachable", "no shrines", "no
  creature banks", given current per-tier overrides. Plug into existing
  inline-validation surface (T-303).
- **Acceptance:** Five canonical misconfig fixtures each surface the
  expected warning; presets stay warning-free.

### T-704 — Per-player fairness audit
- **Status:** done (PR pending)
- **Owner:** Claude
- **Effort:** M
- **Files:** `TemplateAnalysis.cs`, fairness panel in both hosts.
- **Scope:** For each player zone compute neighbor count, starting-castle
  count, expected resource yield (sum of bank values + bonuses); flag any
  player whose values deviate by >X% from the median. Catches asymmetric
  generation in templates that should be mirrored.
- **Acceptance:** A deliberately-asymmetric fixture lights up; balanced
  presets pass clean.

### T-705 — Topology graph stats
- **Status:** done (PR pending)
- **Owner:** Claude
- **Effort:** S
- **Files:** `Services/TemplateAnalysis.Topology.cs` (new sibling partial),
  `Components/TopologyPanel.razor` (+ scoped CSS),
  `Views/TopologyStatsPanel.xaml`/`.xaml.cs`, `MainWindow.xaml`/`.xaml.cs`.
- **Scope:** Reads emitted `Variant.Connections` as an undirected simple
  graph (parallel edges + self-loops deduped). Reports node count, edge
  count, average degree, diameter (BFS, largest component), component
  count, and articulation points (Tarjan's algorithm, iterative DFS).
- **Acceptance:** All ten shipped presets generate non-empty topology
  reports under a fixed seed (pinned in
  `TemplateAnalysisTopologyTests`); hand-built micro-graphs (triangle,
  path, star, bridge-of-triangles, disconnected) verify each metric.

---

## Phase 8 — Editor ergonomics &amp; reach

### T-801 — Seed control + reproducible generation
- **Status:** done (PR TBD)
- **Owner:** —
- **Effort:** S
- **Files:** `GeneratorSettings.cs:445`, `SettingsFile.cs:56`,
  `TemplateGenerator.cs:33`, `MapSettingsPanel.razor:56-65, 138-140`,
  `Home.razor:163-169`, WPF `MainWindow.xaml`/`.xaml.cs`.
- **Scope:** Web side is shipped: `int? Seed` round-trips, generator
  uses `settings.Seed.HasValue ? new Random(settings.Seed.Value) :
  new Random()`, UI exposes seed input + 🎲 randomize and "Seed used: …"
  readout. Two gaps to close: (1) audit every `new Random()` callsite
  inside `TemplateGenerator` to confirm they all flow from the seeded
  instance — the constructor seeds one Random, but ad-hoc allocations
  elsewhere would leak determinism; (2) add the equivalent seed input +
  readout to the WPF host. Closes upstream issue #20 once both ship.
- **Acceptance:** Same seed + same settings produces byte-identical
  output (test). No `new Random()` left in `TemplateGenerator` outside
  the seeded plumbing. WPF surface matches Web.

### T-802 — Undo / redo for settings edits
- **Status:** open
- **Owner:** —
- **Effort:** M
- **Files:** New `Services/EditHistory.cs`, both UI hosts.
- **Scope:** Snapshot `GeneratorSettings` on every change-event; expose
  Ctrl-Z / Ctrl-Y. Cap stack at 50 to bound memory. Lowers the cost of
  experimentation — by far the most common user friction in HoMM template
  authoring.
- **Acceptance:** Manual: a slider tweak followed by Ctrl-Z restores prior
  value across both hosts. Snapshot serialization is identical to the
  in-memory clone.

### T-803 — Inline field help (tooltips driven by docs YAML)
- **Status:** open
- **Owner:** —
- **Effort:** M
- **Files:** New `docs/field-help.yaml`, tooltip components in both hosts.
- **Scope:** Half the obscure flags (`MinNeutralZonesBetweenPlayers`,
  `GuardRandomization`, `EncounterHolesSettings`) have no inline help. Add
  one YAML keyed by `ValidationFieldKeys`-style ids; both hosts read it at
  build time. Upstream issue
  https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/issues/21
  flags Web ↔ WPF tooltip parity.
- **Acceptance:** ≥30 fields documented; tooltip displays the YAML entry on
  hover/focus on both hosts.

### T-804 — Universal picker search
- **Status:** open
- **Owner:** —
- **Effort:** S
- **Files:** Picker components.
- **Scope:** Single search box that filters heroes, spells, units, items,
  SIDs in their respective pickers. Today every picker re-implements ad-hoc
  scrolling.
- **Acceptance:** Search input filters the open picker; works on Web and WPF.

### T-805 — Settings-vs-preset diff view
- **Status:** open
- **Owner:** —
- **Effort:** M
- **Files:** New `Components/PresetDiffPanel.razor`, WPF parity,
  `Services/SettingsDiff.cs`.
- **Scope:** Side-by-side field-level diff between current settings and
  nearest preset. Helps users learn the tool and recover from bad
  exploration. Builds on the per-feature flag registry from T-302.
- **Acceptance:** Loading a preset then changing 3 fields shows exactly
  those 3 in the diff. Empty diff after fresh preset load.

### T-806 — Fill preset archetype gaps (3p / 5p / hub / might-only)
- **Status:** open
- **Owner:** —
- **Effort:** S
- **Files:** `Services/PresetCatalog.cs`,
  `Resources/Presets/presets.json`.
- **Scope:** Today's 10 presets skip 3- and 5-player counts, hub-and-spoke
  topology, and a "no-magic / might-only" archetype. Add `triad-3p`,
  `pentagram-5p`, `hub-defense`, `might-only`.
- **Acceptance:** Each new preset generates without warnings on default
  settings. Each has a one-line description in the picker.

### T-807 — User preset slots (named saves)
- **Status:** open
- **Owner:** —
- **Effort:** S
- **Files:** Both UI hosts (localStorage / `%AppData%`),
  `SettingsFile.cs`.
- **Scope:** Persist named user presets locally; show under a "My Presets"
  section in the preset picker. Independent of the bundled catalog.
- **Acceptance:** Save → reload page/app → preset is still listed and loads
  identical settings.

### T-808 — Web ↔ WPF parity gaps
- **Status:** open
- **Owner:** —
- **Effort:** M
- **Files:** Various components in both hosts.
- **Scope:** Upstream issues
  https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/issues/21
  and
  https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/issues/22
  enumerate parity gaps: GameMode picker hidden on WPF, installer-ZIP save
  is Web-only, `FixedStartingHeroByFaction` round-trips on Web but is
  uneditable. Close them.
- **Acceptance:** Both hosts expose the same set of editable fields for the
  enumerated items. Manual smoke on each.

---

## Out of scope / non-goals

The tool emits `.rmg.json` and stops there. These are explicit non-goals:

- **Scenario-style authoring.** `RmgTemplate.contentPools`/`contentLists`
  authoring, `MandatoryContentGroup` editing, full `ContentItem.rules` trees,
  and `ZoneLayout` authoring all start to look like a content editor rather
  than a generator. We round-trip these without data loss but do not build
  authoring UIs for them. Skill/subclass bans are similarly out — the
  schema has no surface for them (see prior cancellation of T-102).
- Live map preview using game assets (renderer stays schematic).
- Balance simulation, AI playthroughs, win-rate prediction.
- Editing scenario maps (`.h5m`-style content).
- Server-hosted catalog or login-based preset sharing. Share-link codec is
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

---

## Done log

Compact record of completed phases. The detailed entries lived in this file
through T-206; they have been pruned to keep the index navigable. Git history
preserves the full text.

### Phase 1 — Expose schema surface (all done)
- T-001 Connection length / gatePlacement / portal placement / guardEscape /
  simTurnSquad
- T-002 MainObject guardChance + removeGuardIfHasOwner
- T-003 RmgTemplate.valueOverrides
- T-004 Zone.guardReactionDistribution
- T-005 Zone.diplomacyModifier / crossroadsPosition / contentBiome
- T-006 Zone.contentCountLimits / guardCutoffValue / content pool assignments

### Phase 2 — Catalog depth (T-101, T-103, T-104 done; T-102 cancelled)
- T-101 ZoneContentSidCatalog broad coverage
- T-102 Skills + subclasses pickers — **cancelled 2026-05-14**: schema has no
  ban or availability surface for skills/subclasses.
- T-103 More preset archetypes (10 total spanning 2/4/6/8 players)
- T-104 Community-data refresh workflow (weekly GH Action)

### Phase 3 — Generation tuning (all done)
- T-201 Encounter holes (multi-stack battles)
- T-202 Mandatory content placement rules
- T-203 MetaObjectsBiome selectors and themed pools
- T-204 Per-tier-8 neutral creature support — **no-op (verified)**: pickers
  already group by `(Faction, Tier)` dynamically; schema has no tier-8
  guard-pool surface. Regression test added.
- T-205 Per-tier terrain density
- T-206 Per-player Starting Bonuses overrides

### Phase 4 — UX baseline (all done)
- T-301 Mobile layout under 600 px
- T-302 Per-experimental-feature toggles
- T-303 Inline validation remediation
- T-304 Preview zoom + pan + reseed-in-place
