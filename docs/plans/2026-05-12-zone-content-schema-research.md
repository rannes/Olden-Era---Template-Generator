# 2026-05-12 — Zone Content Schema Research

Grounding the next round of zone-content work (the emitter and beyond) in
what the actual `.rmg.json` schema accepts. Companion to:

- `docs/plans/2026-05-11-customizable-zone-content-design.md` — original feature design
- `docs/plans/2026-05-11-customizable-zone-content-impl.md` — implementation plan, paused at end of Phase 2

Sourced from reading four shipped templates: **Hallway**, **Diamond**,
**Anarchy**, **Sprint** (chosen for: small spawn-only, mid-size with factions,
rich/complex, connection-rule examples respectively).

## TL;DR

The emitter is feasible but its scope is much smaller than the original
`ZoneContentItem` shape implied:

- Only the **Mandatory pool** has a useful schema slot for user-authored items.
- Per-item **biome** and **faction** filters do not exist in the schema.
- **Counts** are expressed by repetition, not range selectors.
- Non-Mandatory pools (`guardedContentPool` / `unguardedContentPool` /
  `resourcesContentPool`) reference *named pool definitions*, not raw SID lists.
  Letting users add a single SID to one of these would require synthesising a
  whole new pool definition — out of scope for v1.
- **Connection rules don't live at template top level.** They are entries in
  each zone's `roads[]` block. The original `ContentConnectionRule` DTO design
  needs to be reshaped accordingly.

## What `mandatoryContent` actually looks like

Each `Zone` has `mandatoryContent: ["mandatory_content_X"]` — a list of *names*
that reference top-level `mandatoryContent[]` group definitions. A group has:

```json
{
    "name": "mandatory_content_red",
    "content": [
        { "sid": "mine_crystals", "isMine": true, "isGuarded": false },
        { "sid": "pandora_box", "variant": 9, "soloEncounter": true,
          "rules": [{ "type": "Road", "args": [], "targetMin": 0.1, "targetMax": 0.2, "weight": 1 }] },
        { "sid": "pandora_box", "variant": 9, "name": "name_pandora_box_army",
          "isGuarded": true,
          "rules": [{ "type": "Road", "args": [], "targetMin": 0.8, "targetMax": 0.9, "weight": 1 },
                    { "type": "Crossroads", "args": [], "targetMin": 0.8, "targetMax": 0.9, "weight": 2 }],
          "soloEncounter": true },
        { "sid": "mana_well", "name": "name_mana_well", "isGuarded": false },
        { "includeLists": ["content_list_building_random_hires_high_tier"] }
    ]
}
```

(From `Sprint.rmg.json` lines 964–998.)

Per-item fields seen across the four templates:

| Field | Type | Notes |
| --- | --- | --- |
| `sid` | string | The thing to place. Mutually exclusive with `includeLists`. |
| `name` | string? | Optional. Required if other parts of the template need to reference this item (e.g., `roads`). |
| `isGuarded` | bool? | If true, an encounter is rolled before placement. |
| `isMine` | bool? | Marks this as a mine for placement-density purposes. |
| `variant` | int? | Variant selector for things like `pandora_box`. |
| `soloEncounter` | bool? | Single-encounter version. |
| `rules` | list of ContentPlacementRule? | Constraints on where this item can land. |
| `includeLists` | list of strings? | Embed entries from named content lists. Mutually exclusive with `sid`. |

## What `ContentPlacementRule` actually looks like

```json
{ "type": "Road", "args": [], "targetMin": 0.1, "targetMax": 0.2, "weight": 1 }
```

Observed `type` values across the four templates:

- `"Road"` — distance from the road network (args: []). `targetMin`/`Max` are
  proportions in arc-length space.
- `"MainObject"` — distance from a numbered MainObject on the zone (args:
  ["0"] for the spawn/primary city). Same target-range semantics.
- `"Crossroads"` — distance from any crossroads in the zone (args: []).
- `"Connection"` — distance from a named connection (args: ["Spawn-A-Hallway-1"]).
- `"Sid"` — distance from another item with this SID (args: ["remote_foothold"]).

`weight` (typically 1, sometimes 2) controls how strongly this rule fights
against the others when the placer is searching for a spot.

`targetMin` / `targetMax` are non-negative floats. Common ranges:

- Very close: 0.05–0.15 (e.g., watchtower near MainObject).
- Close: 0.10–0.20.
- Medium: 0.15–0.30.
- Far: 0.30–0.50 or 0.50–0.75.
- Off-network (placement-suppression): 0.0–0.0 with weight 1.

## What `roads[]` actually looks like (the "connection rules")

The original design had a `ContentConnectionRule` top-level type. The schema
puts road decoration *inside each zone*:

```json
"roads": [
    { "type": "Stone",
      "from": { "type": "Connection", "args": ["Spawn-A-Red-A"] },
      "to":   { "type": "MandatoryContent", "args": ["name_mana_well"] } },
    { "type": "Stone",
      "from": { "type": "MandatoryContent", "args": ["name_mana_well"] },
      "to":   { "type": "Connection", "args": ["Red-A-Orange-A"] } },
    { "type": "Dirt",
      "from": { "type": "MandatoryContent", "args": ["name_mana_well"] },
      "to":   { "type": "MandatoryContent", "args": ["name_pandora_box_army"] } }
]
```

(From `Sprint.rmg.json:225–230`.)

Selectors (`from` / `to`) accept:

- `{ "type": "Connection", "args": ["<connection-name>"] }`
- `{ "type": "MainObject", "args": ["<index>"] }`
- `{ "type": "MandatoryContent", "args": ["<named-mandatory-item>"] }`

Top-level rule `type` is the road kind (`"Stone"`, `"Dirt"`).

This shape is rule-of-three smaller than what the original
`ContentConnectionRule` DTO modelled. There is no `MinDistance`/`MaxDistance`
on a road rule — distance constraints are expressed via `ContentPlacementRule`
on the items themselves.

## Per-item biome and faction — no slots

Searched all four templates plus a wider grep. `biome` and `faction` only ever
appear at zone level (`zoneBiome`, `contentBiome`, `metaObjectsBiome`) or on
`mainObjects[]` entries (the spawn/city objects), never on individual
`mandatoryContent[].content[]` items.

Practical implication: `ZoneContentItem.BiomeFilter` and
`ZoneContentItem.FactionAffinity` cannot be emitted today. They round-trip
through `.oetgs` / share-link harmlessly (so a future schema could pick them
up), but the emitter must drop them with a typed warning.

## Non-Mandatory pools — referenced, not inlined

`guardedContentPool: ["content_pool_template_hallway_guarded_start_zone"]`
points at a top-level `contentPools[]` definition (or, for shipped templates,
an external JSON in `GameData/GeneratorData/content_pools/`). A pool looks like:

```json
{
    "name": "content_pool_template_hallway_guarded_start_zone",
    "valueDistribution": { "priceBounds": [3999, 6999, 9999], "weights": [4, 17, 11, 0] },
    "groups": [
        { "weight": 10000, "includeLists": ["content_list_pickup_random_items"],
          "content": [
              { "sid": "random_item_common", "weight": 100 },
              { "sid": "random_item_rare", "weight": 80 },
              ... ] },
        ...
    ]
}
```

Adding a single user-authored SID to a guarded/unguarded/resources pool would
require either:

1. Synthesising a brand-new pool definition with an appropriate
   `valueDistribution` and group structure, OR
2. Loading the existing referenced pool's JSON, mutating its `groups`, and
   writing it back.

Both are heavy. Neither belongs in v1 of "let users author MandatoryContent
rows from a UI." The clean cut is:

> v1 only writes to the Mandatory pool. The `Pool` enum's other three values
> are accepted in the DTO (round-trip preserves them) but the emitter drops
> them with a typed warning.

## Knob-by-knob mapping table

For each `ZoneContentItem` field, what the emitter actually does in v1:

| Field | Mandatory pool | Other pools |
| --- | --- | --- |
| `Sid` (string) | Direct → `sid` on emitted content item | Drop + warn (pool reference, no SID slot) |
| `IsGroup` (bool) | If true: emit `{ "includeLists": [Sid] }` instead of `{ "sid": Sid }` | Drop + warn |
| `MinCount` / `MaxCount` (int) | Repeat the emitted entry `MaxCount` times. If `MinCount != MaxCount`, emit `MaxCount` and warn (no count selector in schema) | Drop + warn |
| `Pool` (enum) | Routes to per-zone Mandatory group | Routes to "drop + warn" |
| `IsGuarded` (bool) | Direct → `isGuarded` | Drop + warn |
| `NearCastle` (bool) | If true, append placement rule `{ "type": "MainObject", "args": ["0"], "targetMin": 0.05, "targetMax": 0.25, "weight": 1 }` | Drop + warn |
| `RoadDistance` ("Close"/"Mid"/"Far") | Append placement rule `{ "type": "Road", "args": [], "targetMin": …, "targetMax": …, "weight": 1 }` with ranges from the established generator pattern | Drop + warn |
| `FactionAffinity` (List) | Drop + warn (no schema slot) | Drop + warn |
| `BiomeFilter` (List) | Drop + warn (no schema slot) | Drop + warn |

## Connection rules — design correction

The original design's `ContentConnectionRule` shape doesn't match the schema.
Replace it with a flatter type that mirrors `roads[]`:

```csharp
public sealed class ZoneRoadDecoration
{
    public string Zone { get; set; } = "";          // owning zone name; UI-only
    public string RoadType { get; set; } = "Stone"; // "Stone" | "Dirt" | etc
    public RoadEndpoint From { get; set; } = new();
    public RoadEndpoint To { get; set; } = new();
}

public sealed class RoadEndpoint
{
    public string Kind { get; set; } = "";          // "Connection" | "MainObject" | "MandatoryContent"
    public string Arg { get; set; } = "";           // single arg (the only shape observed)
}
```

Drop: `Type` (Distance/OnRoad/Between), `MinDistance`, `MaxDistance` —
these were modelled but the schema doesn't have them. Distance constraints
belong on the item's `rules` (which is what `RoadDistance`/`NearCastle` now
emit), not on the road decoration.

Plan a small follow-up commit that retires `ContentConnectionRule` /
`ContentRuleType` and introduces `ZoneRoadDecoration` /
`RoadEndpoint`. Settings field renames from `ContentConnectionRules` to
`ZoneRoadDecorations`.

## Open questions for the emitter brainstorm

1. **Per-zone Mandatory group selection.** The generator currently builds one
   `mandatory_content_<scope>_<letter>` group per zone. Does the emitter
   *append to* the existing per-zone group, or does it create a new group
   `mandatory_content_user_<scope>_<letter>` and add the name to
   `Zone.MandatoryContent`? Both work. Appending is simpler and the schema
   accepts it; a new group is cleaner to identify in the diff. Lean: **append**.

2. **Item naming.** The emitter must auto-name a user-authored item when
   another rule references it (e.g., a road decoration referencing
   `MandatoryContent name_x`). Naming convention: `name_user_<sid>_<index>`
   so multiple instances disambiguate.

3. **`ZoneContentSidCatalog` expansion.** The current 4-entry seed covers
   mana wells and pandora boxes. Real users will want mines, dwellings,
   utility buildings (watchtower, market). Source these from the existing
   `KnownIds` constants we already have? Yes — and tag each by category
   ("Mine", "Mandatory", "Utility", "Loot") so the picker groups them.

4. **Validator extension.** Today's `ZoneContentItemValidator` only checks
   self-consistency. Add a "schema-emit warnings" pass that surfaces
   "BiomeFilter has no schema slot — will be ignored" type messages so the
   UI can show them inline before the user tries to generate.

5. **DTO surface follow-ups (from PR #10 review).** Worth doing in the same
   round so the DTO stabilises before any UI references it:
   - Rename `ContentConnectionRule`/`ContentRuleType` → see correction
     above. Replaces the type entirely.
   - Rename `ContentPreset`/`ContentPresets` → `ZoneContentPreset`(`s`)
     for consistency with the `ZoneContent*` prefix.
   - Convert `RoadDistance` from `string?` to `RoadDistance?` enum
     (validator already restricts to Close/Mid/Far).

## Implications for the round shape

Original "Phase 3" was monolithic emitter + generator wiring. With the
findings above, the cleanest round is:

**Round 2 (next session): Emitter and DTO follow-ups, library only.**

- Rename DTOs per question 5.
- Replace `ContentConnectionRule` with `ZoneRoadDecoration` / `RoadEndpoint`.
- Build `ZoneContentEmitter.ApplyToMandatoryGroup(group, list, ctx)` for the
  Mandatory pool. Drop-and-warn for the other pools.
- Build `ZoneRoadDecorationEmitter.ApplyToZone(zone, decorations)`.
- Add `EmitWarning` surface and pin warnings in the validator for "this
  knob will be ignored".
- Wire into `BuildSpawnZone` / `BuildNeutralZone` with empty-list fallback.
- Snapshot test: empty user inputs → byte-identical output.
- End-to-end: populated user inputs → expected items appear in the right
  Mandatory group + expected road decorations on the right zone.

No host UI, no `.oetgs`, no share-codec changes in Round 2.

**Round 3 (later): persistence + share-codec.** SettingsMapper, share fixture.

**Round 4 (later): Web UI, then WPF UI.** Master+detail panels, presets,
inspect-defaults, experimental gate.

This slicing keeps each round to a clean, reviewable MR informed by what we
now know. Round 2 is the keystone — once it lands, the feature has a
working spine even if no UI exists yet.
