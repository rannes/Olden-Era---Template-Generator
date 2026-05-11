# 2026-05-11 — Customizable Zone Content (design)

Brings upstream PR #17 ("Customise player starting zone contents",
`0c45c42` → `edb64cb`) into our fork, expanded in scope, reshaped onto our existing
`GameDataCatalog` / `KnownIds` / `CommunityCatalog`, and ported to both WPF and Blazor
hosts. Single all-in-one MR per the rollout decision.

Companion to `docs/plans/2026-05-11-upstream-sync.md`. That doc explains *why* this is
its own initiative; this one is the spec.

## Goals

1. Users can customise the contents of player Spawn zones and neutral zones.
2. Customisation is layered: a Global neutral list, then per-tier overrides
   (Poor / Normal / Rich), then per-zone-letter overrides (Red-A, Orange-B, …).
   Player Spawn zones get one uniform list (no per-letter axis in v1).
3. Each entry exposes nine knobs: SID (catalog-picked, free-text fallback),
   MinCount, MaxCount, Pool (Mandatory / Guarded / Unguarded / Resources),
   IsGuarded, NearCastle, RoadDistance, FactionAffinity (multi-faction or
   "match player"), BiomeFilter (multi-select). Plus an `IsGroup` flag for
   `includeLists` references.
4. Users can author connection rules between items / connection IDs (mana well
   on the Stone road between Spawn-A and Red-A, etc.).
5. Both WPF and Blazor hosts get a new top-level **Zone Content** panel
   with three tabs: Player, Neutral, Connection Rules.
6. Share-link codec round-trips the new fields without bumping version.
7. Feature is gated behind the existing Experimental toggle.
8. Empty user lists trigger fallback to today's hard-coded behaviour — zero
   regression for unmodified templates.

## Non-goals (deferred)

- Graph-canvas visualization for connection rules — list-of-rules in v1; canvas
  is post-MVP, no data-model change required.
- Editing the generator's *internal* hard-coded pool defaults beyond per-row
  pool tagging.
- Per-zone-letter overrides for player Spawn zones (uniform list is enough).

## Data model

Library-side, in `OldenEra.Generator/Models/Generator/`:

```csharp
public enum ZoneContentPool { Mandatory, Guarded, Unguarded, Resources }
public enum NeutralZoneTier { Poor, Normal, Rich }

public sealed class ContentItem
{
    public string Sid { get; set; } = "";
    public bool IsGroup { get; set; }                       // includeLists ref vs single SID
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public ZoneContentPool Pool { get; set; } = ZoneContentPool.Mandatory;
    public bool IsGuarded { get; set; }
    public bool NearCastle { get; set; }
    public string? RoadDistance { get; set; }               // "Close" | "Mid" | "Far" | null
    public List<string> FactionAffinity { get; set; } = new();  // empty = any; "MATCH_PLAYER" sentinel
    public List<string> BiomeFilter { get; set; } = new();      // empty = any
}

public sealed class ZoneContentList
{
    public List<ContentItem> Items { get; set; } = new();
}

public sealed class NeutralZoneContent
{
    public ZoneContentList Global { get; set; } = new();
    public Dictionary<NeutralZoneTier, ZoneContentList> ByTier { get; set; } = new();
    public Dictionary<string, ZoneContentList> ByZoneLetter { get; set; } = new(); // "Red-A" => list
}

public enum ContentRuleType { Distance, OnRoad, Between }
public sealed class ContentConnectionRule
{
    public ContentRuleType Type { get; set; }
    public string FromRef { get; set; } = "";   // SID or "Connection:Spawn-A-Red-A"
    public string ToRef   { get; set; } = "";
    public string? RoadType { get; set; }       // "Stone" | "Dirt" | null
    public double? MinDistance { get; set; }
    public double? MaxDistance { get; set; }
}
```

Three new fields on `GeneratorSettings`:

```csharp
public ZoneContentList PlayerZoneContent { get; set; } = new();
public NeutralZoneContent NeutralZoneContent { get; set; } = new();
public List<ContentConnectionRule> ContentConnectionRules { get; set; } = new();
```

Mirror in `SettingsFile` with JSON names `playerZoneContent`,
`neutralZoneContent`, `contentConnectionRules`. All optional in JSON; defaults
are empty.

### Resolution semantics

For a neutral zone of tier `T` and letter `L`, generator merges
`Global` ⊕ `ByTier[T]` ⊕ `ByZoneLetter[L]`. Append in order; same-`Sid` entries
from later layers *replace* (not duplicate) earlier ones.

Inheritance for the UI: a row coming from a higher scope is shown read-only with
"(inherited from Global)" or "(inherited from Tier: Normal)" label and an
"Override here" affordance.

## Library architecture

New namespace `OldenEra.Generator.Services.ZoneContent/`:

| File | Responsibility |
| --- | --- |
| `ZoneContentResolver.cs` | Merge `Global ⊕ ByTier ⊕ ByZoneLetter` for a zone. |
| `ZoneContentEmitter.cs` | Write a resolved list onto `Zone.MandatoryContent` / pool fields. |
| `ContentItemValidator.cs` | Catch invalid SIDs, contradictory knobs, count<0. |
| `ContentPresets.cs` | Built-in curated `ContentItem` rows users can insert. |
| `ConnectionRuleEmitter.cs` | Mutate `Zone.Connections` from rule list. |
| `ZoneContentSidCatalog.cs` | Joins `GameDataCatalog` + `CommunityCatalog`, tags entries with friendly name + category for the picker. |

We do **not** adopt upstream's `Services/ContentManagement/SidMapping.cs`,
`ContentPresets.cs`, or `ContentIds.cs`. Their role is replaced by the existing
catalog layer plus the new `ZoneContentSidCatalog` adapter — one source of truth
for SIDs.

### Generator wiring

In `TemplateGenerator.BuildSpawnZone`:

```csharp
var resolved = settings.PlayerZoneContent;
if (resolved.Items.Count == 0)
{
    // existing hard-coded MandatoryContent / pools — unchanged
}
else
{
    ZoneContentEmitter.Apply(zone, resolved, ctx);
}
```

In `TemplateGenerator.BuildNeutralZone`:

```csharp
var resolved = ZoneContentResolver.Resolve(
    settings.NeutralZoneContent, plan.Quality, plan.Letter);
// ...same fallback / apply pattern
```

After zone construction, `ConnectionRuleEmitter.Apply(template, settings.ContentConnectionRules)`
runs once and mutates `Connections` entries to add `from` / `to` / `type` triples
in the same shape as the existing `Sprint.rmg.json` reference template.

### Pool routing

`ZoneContentEmitter` routes each `ContentItem` based on `Pool`:

| Pool | Target field |
| --- | --- |
| Mandatory | `Zone.MandatoryContent` |
| Guarded | `Zone.GuardedContentPool` |
| Unguarded | `Zone.UnguardedContentPool` |
| Resources | `Zone.ResourcesContentPool` |

`MinCount` / `MaxCount` map to `.rmg.json` count selectors via the existing
`Random` typed selector. `FactionAffinity` with `"MATCH_PLAYER"` emits
`{ "type": "Match", "args": ["0"] }`; multi-faction emits
`{ "type": "FromList", "args": [...] }`; empty stays as today.

## Settings, share codec

`SettingsMapper` gains three round-trippers
(`MapPlayerZoneContent`, `MapNeutralZoneContent`, `MapContentConnectionRules`).

`SettingsShareCodec`: same v1 envelope, additive optional fields. The pinned v1
fixture continues to round-trip because the encoder only emits non-empty lists.
A new pinned fixture is added with all three lists populated.

## WPF host

New file: `src/OldenEra.TemplateEditor/Panels/ZoneContentPanel.xaml(.cs)`.

- Section nav adds "Zone Content" between "Heroes" and "Win Conditions".
- `TabControl`: Player / Neutral / Connection Rules.
- **Player tab.** `ListBox` (master) on the left, detail `Grid` on the right.
  Master shows SID + summary chips. Detail has:
  - SID `ComboBox` (catalog-backed, IsEditable=true for free-text fallback)
  - Min / Max numerics
  - Pool `ComboBox`
  - Guarded / NearCastle `CheckBox`es
  - RoadDistance `ComboBox`
  - FactionAffinity multi-select with "Match player" toggle (reuses pattern from banned-units grid)
  - BiomeFilter multi-select
  - "Mark as group" `CheckBox`
  - Buttons: Add, Add preset…, Inspect defaults, Remove, Move up / Move down.
- **Neutral tab.** Same shape, plus a scope picker at top
  (`Global | Poor | Normal | Rich | Red-A | Orange-A | …`). Master list shows
  rows for the active scope, with inherited rows in muted style.
- **Connection Rules tab.** `ListBox` of rules + detail panel:
  - From / To: two-step picker (Item SID or Connection ID, then the ref)
  - Type enum (Distance / OnRoad / Between)
  - RoadType enum
  - MinDistance / MaxDistance numerics
- All controls use the existing `ModernDarkTheme`.

## Blazor host

New folder: `src/OldenEra.Web/Pages/ZoneContent/` with
`ZoneContentPanel.razor`, `PlayerZoneContent.razor`, `NeutralZoneContent.razor`,
`ConnectionRules.razor`, plus shared child components for the row detail editor
and the scope picker.

- Master list is a `<ul>` with chip summaries; detail panel is a flex column of
  stock `<input>` / `<select>`.
- SID picker reuses the typeahead pattern from `SpellPickerComponent.razor`.
- Multi-selects reuse the banned-units grid pattern.
- Connection Rules: list-of-rules with structured ref pickers in v1; canvas
  visualization deferred (no data-model change required to add it later).

## Experimental gate

Panel hidden unless `Settings.ExperimentalFeaturesEnabled`. Same wiring pattern
as `project_experimental_features.md`.

## Tests

Library:

1. **Empty-list parity snapshot.** Emit Jebus Cross with empty user lists →
   byte-equal to today's output. Locks the fallback guarantee.
2. **Resolver merge ordering.** `Global ⊕ ByTier ⊕ ByZoneLetter` with
   same-SID replacement.
3. **Emitter pool routing.** Each `Pool` value writes to the right `Zone` field.
4. **Connection rule emission.** Match the shape used in
   `Sprint.rmg.json:227` (`{ "type": "Stone", "from": ..., "to": ... }`).
5. **Validator coverage.** Unknown SID, `MaxCount < MinCount`, contradictory
   `NearCastle && RoadDistance == "Far"`.
6. **Settings round-trip.** `GeneratorSettings ↔ SettingsFile ↔ JSON`.
7. **Share codec round-trip.** New pinned fixture with all three lists
   populated; existing v1 empty fixture still decodes.
8. **Faction-affinity Match emits sentinel correctly.**

Hosts: smoke tests only — opening the panel, adding a row, persisting via
`.oetgs` / share-link, reloading.

## Rollout

Single MR. Realistic diff size 4–6k LOC. Order of changes within the MR:

1. Library DTOs + `ZoneContent/` namespace + tests 1-5, 8.
2. `TemplateGenerator` wiring with empty-list fallback.
3. `SettingsMapper` + `SettingsShareCodec` + tests 6, 7.
4. WPF `ZoneContentPanel`.
5. Blazor `ZoneContent/` pages.
6. Experimental gate wiring.
7. `UPSTREAM.md` row updates: mark
   `0c45c42` / `55a31a4` / `f47fae3` / `4a76ce2` / `edb64cb` as **ported**.

## Open questions parked for implementation

- Do `ContentItem` rows allow `MinCount == 0` (i.e. "this item is allowed but
  not required")? Lean yes, with validator warning that 0-min entries can be
  omitted by the generator.
- Connection-rule referencing a connection ID: how do we keep the picker in
  sync with the topology user has selected? Probably refresh the picker
  options whenever topology changes; stale refs become validation warnings.
- Inspect-defaults UX: do we render the hard-coded defaults as actual
  `ContentItem` rows, or just as a read-only text dump? Lean towards rows
  (with "Copy to my list") so the affordance composes with everything else.
