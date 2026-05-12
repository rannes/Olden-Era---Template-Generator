# 2026-05-13 — Zone Content Round 3 Design

Round 3 of the customizable zone-content feature. **Library + share-codec
only — no host UI work.** Adds `.oetgs` persistence for the Round 2
zone-content surface, extends the share-codec to cover it, and clears the
small set of follow-ups flagged by the Round 2 final review.

Companion to:

- `2026-05-12-zone-content-schema-research.md` — schema findings
- `2026-05-12-zone-content-round-2-design.md` — Round 2 design + "Implementation deviations"
- `2026-05-12-zone-content-round-2-impl.md` — Round 2 task list (all merged)

## Round goals

1. Make `PlayerZoneContent`, `NeutralZoneContent`, and `ZoneRoadDecorations` round-trip through `.oetgs` (`SettingsFile` + `SettingsMapper`).
2. Make the same three surfaces round-trip through `SettingsShareCodec`.
3. Move every Generator enum onto the JSON string representation by default, so the persisted forms are self-describing.
4. Enum-ify `ZoneRoadDecoration.RoadType` (`"Stone"` / `"Dirt"`) before persistence ships, so the wire form lands as an enum, not a string.
5. Decouple schema strings from enum identifiers in `ZoneRoadDecorationEmitter` via explicit `KindToSchemaType` / `RoadTypeToSchemaType` switches.
6. Pin behaviour for an unmatched `MandatoryContent` endpoint arg in an integration test.

Single MR. Reviewer pass at the end.

## Decisions log

| # | Question | Decision | Reasoning |
| --- | --- | --- | --- |
| 1 | JSON enum policy | **Global `JsonStringEnumConverter`** on every options block touching `SettingsFile`. | No users to protect; `.oetgs` files become self-describing; `JsonStringEnumConverter` reads both ints and strings by default, so existing in-tree presets keep loading. The schema-output path is unaffected (`RmgTemplate` serialisation lives elsewhere). |
| 2 | Enum-ify `RoadType` this round? | **Yes, as the first commit** before any persistence field references it. | Persistence hasn't shipped yet; doing it after would force a string→enum migration in the same round. With Option-1's policy the wire form lands as `"Stone"`/`"Dirt"` either way. |
| 3 | Experimental gate in share-codec? | **No.** Zone-content fields persist and round-trip unconditionally. | Experimental toggles control whether features run, not whether their config persists. Gating risks silent data loss across users. Empty defaults mean non-experimental shares pay no cost. |
| 4 | `KindToSchemaType` — endpoint kind only, or also road type? | **Both.** Adds `KindToSchemaType(ZoneRoadEndpointKind)` and `RoadTypeToSchemaType(ZoneRoadType)` to the emitter. | The decoupling rule is "schema strings are explicit, never derived from enum identifier"; applying it to one enum and not the other is exactly the inconsistency that bites later. |
| 5 | Round-trip test depth | **Fat fixture + reflection guard.** One fully-populated fixture per surface (`SettingsFile`, share-codec) covering every new field; plus a small reflection-based test asserting every public property on `ZoneContentItem`, `ZoneRoadDecoration`, `ZoneRoadEndpoint` is non-default in the fixture. | Catches "added a field, forgot to wire it through the mapper" — the actual failure mode. Reflection guard fails until the fixture is updated. |

## Sequencing

The commits land in this order so each is independently bisectable:

1. **Enum-ify `RoadType`.** New `enum ZoneRoadType { Stone, Dirt }`. `ZoneRoadDecoration.RoadType` retyped. `ZoneRoadDecorationEmitter` updated. Existing tests migrate from string to enum.
2. **Decouple emitter schema strings.** `KindToSchemaType` and `RoadTypeToSchemaType` switches replace `ToString()` calls. Unit-tested by exhaustive cases (one test per enum value × switch).
3. **Integration test: unmatched MandatoryContent arg.** A `ZoneRoadDecoration` whose `MandatoryContent` endpoint `Arg` matches no item `Handle` and isn't an auto-name. Pins: road still emitted, no `Name` field added to any content row.
4. **Global string-enum policy.** Add `JsonStringEnumConverter` to every `JsonSerializerOptions` block touching `SettingsFile`:
   - `SettingsShareCodec.JsonOptions`
   - `SettingsShareCodec.LenientOptions`
   - `PresetCatalog.SettingsOptions`
   - The test-side options blocks in `tests/OldenEra.Generator.Tests/SettingsFileSeedTests.cs`, `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs`, and `tests/OldenEra.Generator.Tests/SeedDeterminismTests.cs`.
   - Re-serialise the three shipped `.oetgs` presets (`arcade-2v2`, `big-map-ffa`, `jebus-like`) so `topology` lands as a string. Reads still accept the int form via the converter's default.
5. **`SettingsFile` fields.** Add `playerZoneContent`, `neutralZoneContent`, `zoneRoadDecorations` to `SettingsFile`. JSON property names are camelCase to match the file's existing convention.
6. **`SettingsMapper` round-trip.** Map both directions between `SettingsFile` and `GeneratorSettings` for the three new fields. Reflection guard test added here.
7. **Share-codec coverage.** No code change required if (4) + (5) are correct (the codec serialises `SettingsFile` directly), but a fat-fixture round-trip test is added under `tests/OldenEra.TemplateEditor.Tests/SettingsShareCodecTests.cs` to pin it.

## Type changes

```csharp
// Models/Generator/ZoneRoadType.cs (new)
public enum ZoneRoadType { Stone, Dirt }

// Models/Generator/ZoneRoadDecoration.cs (changed)
public sealed class ZoneRoadDecoration
{
    public string Zone { get; set; } = "";
    public ZoneRoadType RoadType { get; set; } = ZoneRoadType.Stone;  // was string
    public ZoneRoadEndpoint From { get; set; } = new();
    public ZoneRoadEndpoint To { get; set; } = new();
}
```

`ZoneRoadEndpointKind`, `ZoneContentPool`, `RoadDistance` keep their current
shapes; only their persisted form changes (int → string) when the global
converter is registered.

## Emitter changes

```csharp
// Services/ZoneContent/ZoneRoadDecorationEmitter.cs
private static string KindToSchemaType(ZoneRoadEndpointKind kind) => kind switch
{
    ZoneRoadEndpointKind.Connection       => "Connection",
    ZoneRoadEndpointKind.MainObject       => "MainObject",
    ZoneRoadEndpointKind.MandatoryContent => "MandatoryContent",
    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
};

private static string RoadTypeToSchemaType(ZoneRoadType type) => type switch
{
    ZoneRoadType.Stone => "Stone",
    ZoneRoadType.Dirt  => "Dirt",
    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
};
```

Both replace `e.Kind.ToString()` / `e.RoadType.ToString()` call sites.

## SettingsFile additions

```csharp
[JsonPropertyName("playerZoneContent")]
public ZoneContentList PlayerZoneContent { get; set; } = new();

[JsonPropertyName("neutralZoneContent")]
public NeutralZoneContent NeutralZoneContent { get; set; } = new();

[JsonPropertyName("zoneRoadDecorations")]
public List<ZoneRoadDecoration> ZoneRoadDecorations { get; set; } = new();
```

These are the same types `GeneratorSettings` already exposes — no parallel
DTO. Their members are plain DTOs (no behaviour) and serialise straight.
Default-empty constructions match the `GeneratorSettings` defaults.

## SettingsMapper additions

The mapper has two halves:

```csharp
public static SettingsFile FromSettings(GeneratorSettings s) { ... }
public static GeneratorSettings ToSettings(SettingsFile f)   { ... }
```

Each gains three lines per direction:

```csharp
// FromSettings
file.PlayerZoneContent    = s.PlayerZoneContent;
file.NeutralZoneContent   = s.NeutralZoneContent;
file.ZoneRoadDecorations  = s.ZoneRoadDecorations;

// ToSettings
settings.PlayerZoneContent   = f.PlayerZoneContent   ?? new();
settings.NeutralZoneContent  = f.NeutralZoneContent  ?? new();
settings.ZoneRoadDecorations = f.ZoneRoadDecorations ?? new();
```

The mapper currently doesn't deep-copy the lists — it shares references. We
keep that behaviour for consistency. (If aliasing becomes a concern in
Round 4, the fix lives in the mapper, not in the persistence shape.)

## Share-codec coverage

`SettingsShareCodec.Encode` already calls `JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions)` on a `SettingsFile`. Once (4) and (5) land, the new fields ride along automatically. The added value is a regression test, not new code.

## Tests

In `tests/OldenEra.Generator.Tests/`:

1. **`ZoneRoadDecorationEmitterSchemaMappingTests`** — exhaustive cases for `KindToSchemaType` (3 values) and `RoadTypeToSchemaType` (2 values).
2. **`ZoneRoadDecorationUnmatchedHandleTests`** — `MandatoryContent` endpoint `Arg = "ghost"` with no matching `Handle`/auto-name. Asserts: road emitted, no `Name` on any content row.
3. **`SettingsFileZoneContentRoundTripTests`** — fat fixture: `PlayerZoneContent` with two items (one with `Handle`, both populating every field), `NeutralZoneContent` with one item, three `ZoneRoadDecoration` entries (one per endpoint kind, mixing road types). Serialise → deserialise → assert deep equality. Includes a sub-test asserting `roadDistance` lands as `"Mid"` (string), not `1`.
4. **`SettingsMapperZoneContentRoundTripTests`** — `GeneratorSettings` → `SettingsFile` → JSON → `SettingsFile` → `GeneratorSettings`, deep equality on the three fields.
5. **`SettingsFileZoneContentReflectionGuardTests`** — reflection over `ZoneContentItem`, `ZoneRoadDecoration`, `ZoneRoadEndpoint`; for each public property, asserts the fixture from (3) has a non-default value. Future fields force the fixture forward.

In `tests/OldenEra.TemplateEditor.Tests/`:

6. **`SettingsShareCodecZoneContentTests`** — same fat fixture, `Encode` → `Decode`, deep equality.

## File layout

**New:**

- `src/OldenEra.Generator/Models/Generator/ZoneRoadType.cs`
- The six new test files above.

**Touched:**

- `src/OldenEra.Generator/Models/Generator/ZoneRoadDecoration.cs` — `RoadType` retyped.
- `src/OldenEra.Generator/Services/ZoneContent/ZoneRoadDecorationEmitter.cs` — schema-string switches.
- `src/OldenEra.Generator/Models/Generator/SettingsFile.cs` — three new fields.
- `src/OldenEra.Generator/Services/SettingsMapper.cs` — both directions.
- `src/OldenEra.Generator/Services/SettingsShareCodec.cs` — register `JsonStringEnumConverter` in both options blocks.
- `src/OldenEra.Generator/Services/PresetCatalog.cs` — register converter in `SettingsOptions`.
- `src/OldenEra.Generator/Resources/Presets/{arcade-2v2,big-map-ffa,jebus-like}.oetgs` — re-serialise so `topology` is a string.
- `tests/OldenEra.Generator.Tests/{SettingsFileSeedTests,PresetCatalogTests,SeedDeterminismTests}.cs` — register converter in their local options.

**Deleted:** none.

## Out of scope (Round 4)

- Web UI / WPF UI for zone content.
- Master+detail panels, presets affordance.
- Inspect-defaults, surfacing `EmitWarning` to the user.
- Experimental-gating UX changes (master toggle stays as-is).

## Mac sanity

`dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj` and
`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`
must stay green throughout. The `OldenEra.TemplateEditor.Tests` project also
builds on Mac (no WPF dependency in the test project itself for the share-codec
tests we touch); if it doesn't, the share-codec test moves to
`OldenEra.Generator.Tests` instead. Verify before committing.
