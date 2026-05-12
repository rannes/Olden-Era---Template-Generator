# 2026-05-12 — Zone Content Round 2 Design

Round 2 of the customizable zone-content feature. Library only: emitter,
DTO follow-ups, validator extension. No host UI, no `.oetgs` round-trip,
no share-codec changes.

Companion to:

- `2026-05-11-customizable-zone-content-design.md` — original feature design
- `2026-05-11-customizable-zone-content-impl.md` — original impl plan (Phases 3–9 superseded by this round)
- `2026-05-12-zone-content-schema-research.md` — schema findings that drive every decision below

## Round goals

1. Stabilise the DTO surface (renames, enum migration, retire mismatched types) so future rounds don't churn it.
2. Build `ZoneContentEmitter` for the Mandatory pool. Drop-and-warn for the other three pools.
3. Build `ZoneRoadDecorationEmitter` for the new `Zone.roads[]` shape.
4. Add `EmitWarning` surface so the validator can preview ignorability before generation.
5. Wire both emitters into `TemplateGenerator.BuildSpawnZone` / `BuildNeutralZone` with empty-list fast-paths.
6. Tests: structural no-op guard (empty inputs ⇒ emitters never run) + end-to-end populated case.

Single MR. Self-reviewed by the same agent that writes it.

## Decisions log

Recorded here so the impl plan and future rounds don't re-litigate.

| # | Question | Decision | Reasoning |
| --- | --- | --- | --- |
| 1 | Per-zone Mandatory group: append vs new group? | **Append** to the existing `mandatory_content_<scope>_<letter>` group. | One fewer object, no churn in zone's `mandatoryContent` list, snapshot/no-op test trivial. |
| 2 | How does the user reference items from a road decoration? | **Optional `Handle` field** on `ZoneContentItem`, used verbatim as schema `name` and as `RoadEndpoint.Arg`. Auto-name `name_user_<zone>_<sid>_<index>` only as fallback when an item is referenced but lacks an explicit handle. | Literal, no resolution magic, matches schema mental model 1:1. |
| 3 | Shape of the warning surface? | **Shared `EmitWarning` record** with `Code`/`Message`/`ZoneName`/`Sid`. Single inspector function consumed by both validator (preview) and emitter (during emit). | One source of truth for the rules; surfaces stay in sync. |
| 4 | How to pin the no-op guarantee? | **Structural guard test** (empty inputs ⇒ emitter not invoked) + end-to-end test for populated case. **No** checked-in golden fixture. | Generator output drifts every time another feature lands; fixture maintenance > signal. Structural guard is enough. |
| 5 | Single MR or split? | **Single MR.** | Same agent does change and review; smaller diffs only pay off for human reviewers. |

## DTO surface

New + changed types in `src/OldenEra.Generator/Settings/ZoneContent/`.

### Changed: `ZoneContentItem`

```csharp
public sealed class ZoneContentItem {
    public string Sid { get; set; } = "";
    public bool IsGroup { get; set; }
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public ZoneContentPool Pool { get; set; } = ZoneContentPool.Mandatory;
    public bool IsGuarded { get; set; }
    public bool NearCastle { get; set; }
    public RoadDistance? RoadDistance { get; set; }   // was string?, now enum
    public List<string> FactionAffinity { get; set; } = new();
    public List<string> BiomeFilter { get; set; } = new();
    public string? Handle { get; set; }                // NEW — referenceable name
}

public enum RoadDistance { Close, Mid, Far }
```

### Renamed

- `ContentPreset` → `ZoneContentPreset`
- `ContentPresets` → `ZoneContentPresets`

### Retired

- `ContentConnectionRule` (delete)
- `ContentRuleType` (delete)

### New: road decorations

```csharp
public sealed class ZoneRoadDecoration {
    public string Zone { get; set; } = "";          // owning zone name
    public string RoadType { get; set; } = "Stone"; // "Stone" | "Dirt"
    public RoadEndpoint From { get; set; } = new();
    public RoadEndpoint To { get; set; } = new();
}

public sealed class RoadEndpoint {
    public RoadEndpointKind Kind { get; set; }
    public string Arg { get; set; } = "";
}

public enum RoadEndpointKind { Connection, MainObject, MandatoryContent }
```

### `GeneratorSettings`

- Rename field `ContentConnectionRules` → `ZoneRoadDecorations`, retype to `List<ZoneRoadDecoration>`.

## Emit-warning surface

```csharp
public sealed record EmitWarning(
    string Code,
    string Message,
    string? ZoneName,
    string? Sid);

public static class ZoneContentEmitWarnings {
    public static IReadOnlyList<EmitWarning> Inspect(
        ZoneContentItem item, string? zoneName);
}
```

Codes (string consts on the type for stable references):

| Code | Fires when | Emitter behaviour |
| --- | --- | --- |
| `BiomeFilter.Ignored` | `BiomeFilter.Count > 0` | Emit row, drop the filter |
| `FactionAffinity.Ignored` | `FactionAffinity.Count > 0` | Emit row, drop the filter |
| `Pool.NonMandatoryDropped` | `Pool != Mandatory` | **Skip the item entirely** |
| `MinCount.RangeNarrowedToMax` | `MinCount != MaxCount` | Emit `MaxCount` copies |

Severity: not modelled in v1; UI can map per-code. Validator gains `InspectEmit(item, zoneName)` that delegates to the inspector — existing self-consistency validation stays separate (errors block; warnings preview-and-proceed).

## Mandatory-pool emitter

```csharp
public static class ZoneContentEmitter {
    public static EmitResult ApplyToMandatoryGroup(
        MandatoryContentGroup group,
        IReadOnlyList<ZoneContentItem> items,
        string zoneName,
        IReadOnlySet<(string zone, string sid, int index)> referencedItems);

    public sealed record EmitResult(IReadOnlyList<EmitWarning> Warnings);
}
```

Per-item algorithm:

1. `warnings.AddRange(ZoneContentEmitWarnings.Inspect(item, zoneName))`.
2. If any warning is `Pool.NonMandatoryDropped` → skip emission.
3. Build the row:
   - Body: `IsGroup` ⇒ `{ includeLists: [Sid] }`, else `{ sid: Sid }`.
   - Name: prefer `item.Handle`. Else if `(zoneName, item.Sid, occurrence-index)` is in `referencedItems`, emit `name_user_<zoneName>_<sid>_<index>`. Else no name.
   - `isGuarded = item.IsGuarded` (omit when false; matches observed templates).
   - Placement rules (only attach `rules` array when non-empty):
     - `NearCastle` ⇒ `{ type: MainObject, args: ["0"], targetMin: 0.05, targetMax: 0.25, weight: 1 }`
     - `RoadDistance.Close` ⇒ `{ type: Road, targetMin: 0.10, targetMax: 0.20, weight: 1 }`
     - `RoadDistance.Mid` ⇒ `{ ..., targetMin: 0.30, targetMax: 0.50, weight: 1 }`
     - `RoadDistance.Far` ⇒ `{ ..., targetMin: 0.60, targetMax: 0.85, weight: 1 }`
4. Append the row `MaxCount` times to `group.Content`.

Mutation in place is safe: the per-zone group is created fresh in `BuildSpawnZone` / `BuildNeutralZone` each generation.

## Road-decoration emitter

```csharp
public static class ZoneRoadDecorationEmitter {
    public static void ApplyToZone(
        Zone zone,
        IReadOnlyList<ZoneRoadDecoration> decorationsForThisZone);

    public static IReadOnlySet<(string zone, string sid, int index)> ReferencedItems(
        IReadOnlyList<ZoneRoadDecoration> decorations,
        IReadOnlyDictionary<string, IReadOnlyList<ZoneContentItem>> itemsByZone);
}
```

Per-decoration: append `{ type: RoadType, from: { type: From.Kind, args: [From.Arg] }, to: { type: To.Kind, args: [To.Arg] } }` to `zone.Roads`.

No warnings from this emitter — the DTO is 1:1 with the schema. Structural problems (unknown kind, empty arg) are validator hard errors before we get here.

`ReferencedItems` walks decorations, finds `MandatoryContent` endpoints, and resolves each `Arg` against the items lists for the relevant zones. If `Arg` matches an item's `Handle`, the handle is used as `name`. If `Arg` doesn't match a handle, fall back to interpreting `Arg` as a literal auto-name (the user is expected to use handles for v1; this fallback exists only so a future user could write the literal name if they choose).

## Generator wiring

In `TemplateGenerator.BuildSpawnZone` and `BuildNeutralZone`, after the per-zone Mandatory group is built and after the existing `roads[]` are populated:

```csharp
var userItems = ResolveUserItems(zone, settings);
if (userItems.Count > 0) {
    var referenced = ZoneRoadDecorationEmitter.ReferencedItems(
        settings.ZoneRoadDecorations, allUserItemsByZone);
    var result = ZoneContentEmitter.ApplyToMandatoryGroup(
        group, userItems, zone.Name, referenced);
    warnings.AddRange(result.Warnings);
}

var userDecorations = settings.ZoneRoadDecorations
    .Where(d => d.Zone == zone.Name).ToList();
if (userDecorations.Count > 0) {
    ZoneRoadDecorationEmitter.ApplyToZone(zone, userDecorations);
}
```

Empty-list fast paths give the no-op guarantee.

`ResolveUserItems` is the existing `ZoneContentResolver` call — already in place from Round 1.

## Tests

In `tests/OldenEra.Generator.Tests/Settings/ZoneContent/`:

1. **`ZoneContentEmitWarningsTests`** — one case per code (5): biome ignored, faction ignored, non-mandatory dropped, count narrowed, clean item zero warnings.
2. **`ZoneContentEmitterTests`**:
   - Sid-only item ⇒ `{ sid }`, no extra fields.
   - `IsGroup = true` ⇒ `{ includeLists: [Sid] }`.
   - `MaxCount = 3` ⇒ row repeated 3 times.
   - `Handle = "x"` ⇒ `name = "x"`.
   - Referenced-but-no-handle ⇒ auto-name `name_user_<zone>_<sid>_<idx>`.
   - `NearCastle = true` + `RoadDistance = Mid` ⇒ both placement rules with expected ranges.
   - `Pool = Guarded` ⇒ no row emitted, warning surfaced.
3. **`ZoneRoadDecorationEmitterTests`**:
   - All three endpoint kinds × `Stone`/`Dirt` ⇒ expected `roads[]` entry.
   - `ReferencedItems` finds `MandatoryContent` endpoints, ignores others.
4. **`TemplateGenerator` integration**:
   - **No-op guard**: empty `PlayerZoneContent` + `NeutralZoneContent` + `ZoneRoadDecorations` ⇒ emitters never invoked.
   - **End-to-end**: populated `PlayerZoneContent` with one mana well + one road decoration referencing it via `Handle` ⇒ expected mandatory row with `name`, expected `roads[]` entry on the right zone.
5. **`ZoneContentItemValidator.InspectEmit`** — delegates to inspector; existing tests migrate from `string` `RoadDistance` to enum.

## File layout

**New:**
```
src/OldenEra.Generator/Settings/ZoneContent/
  EmitWarning.cs
  ZoneContentEmitWarnings.cs
  ZoneContentEmitter.cs
  ZoneRoadDecorationEmitter.cs
  ZoneRoadDecoration.cs
  RoadEndpoint.cs
  RoadDistance.cs
```

**Touched:**
- `ContentPreset.cs` / `ContentPresets.cs` ⇒ `ZoneContentPreset.cs` / `ZoneContentPresets.cs` (file + class rename)
- `ZoneContentItem.cs` (add `Handle`, retype `RoadDistance`)
- `GeneratorSettings.cs` (rename + retype field)
- `ZoneContentItemValidator.cs` (drop string-based `RoadDistance` validation, add `InspectEmit`)
- `TemplateGenerator.cs` (wire emitters into both zone builders)

**Deleted:**
- `ContentConnectionRule.cs`
- `ContentRuleType.cs`

## Out of scope (Round 3 / 4)

- `.oetgs` settings file round-trip (`SettingsMapper` updates).
- Share-codec changes / fixture.
- Web UI master+detail panels, presets, inspect-defaults.
- WPF host UI.
- Experimental-feature gating UX (the existing master toggle remains; gate stays where it is).

## Mac sanity

`dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj` and `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` are the two commands that must stay green throughout. WPF host project is not touched in Round 2.
