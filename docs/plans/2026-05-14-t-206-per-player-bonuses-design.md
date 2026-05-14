# T-206 — Per-player Starting Bonuses (design)

Date: 2026-05-14
Status: approved, in implementation

## Problem

The experimental Starting Bonuses panel emits a single block of bonuses applied
uniformly to all players (`ReceiverSide = -1`). The `Bonus` schema already
supports per-player targeting via `ReceiverSide` (player index) and
`ReceiverFilter` ("start_hero" | "all_heroes"), but the generator never sets
`ReceiverSide` to a concrete slot. Templates that want asymmetric starting
conditions — mirror matchups with one side starting with extra resources, draft
formats, handicap modes — cannot be expressed.

## Goal

Let users author a per-player override table on top of the existing uniform
block. Each override row targets one player slot and replaces specific fields
of the uniform bonus for that slot only. Defaults must be byte-identical to
current output; the feature is gated behind the existing starting-bonuses
experimental flag.

## Non-goals

- Per-team filtering. The schema's `ReceiverFilter` only accepts
  `start_hero` / `all_heroes` for the bonuses we emit; team-scoped filters are
  not exposed today and are out of scope.
- Conditional bonuses (turn-based, event-triggered). Not in the schema.
- Reworking the uniform block's data shape.

## Data model

Reuse `StartingBonusSettings` for each per-player row, plus a `PlayerSlot`:

```csharp
public class PerPlayerBonusOverride
{
    public int PlayerSlot { get; set; } = 1;          // 1..PlayerCount
    public StartingBonusSettings Bonuses { get; set; } = new();
}

// added on StartingBonusSettings
public List<PerPlayerBonusOverride> PerPlayerOverrides { get; set; } = new();
```

Empty list → current behavior, byte-identical output.

Reusing `StartingBonusSettings` means new bonus fields added later
automatically flow through to per-player rows without a parallel mirror.

## Override semantics: replace per field

For each field on the embedded `StartingBonusSettings` that is "set" (non-zero
int, non-empty string, non-zero double), the override row replaces the uniform
value for its slot only. Fields the row leaves at sentinel defaults inherit the
uniform value.

Example: uniform sets `gold = 1000` and `HeroAttack = 2`. Slot 2 override sets
`HeroAttack = 5` only. Result:

- Slot 2 receives `gold = 1000` (uniform) and `HeroAttack = 5` (override).
- All other slots receive `gold = 1000` and `HeroAttack = 2`.

Duplicate `PlayerSlot` rows: **last-write-wins** at the row level. Validator
warns. Standard config-overlay precedence (JSON merge patch, K8s patch, env
vars).

## Emission strategy

For each field that the uniform block would emit:

- If no override row sets that field: emit one `Bonus` with `ReceiverSide = -1`
  (current behavior).
- If any override row sets that field for some slot: emit one `Bonus` per
  player slot (1..PlayerCount), each with `ReceiverSide = slot` and the
  effective value (override-or-uniform). Do **not** emit the
  `ReceiverSide = -1` row for that field.

Emission stays in `BuildExperimentalBonuses`, called once after `Generate()`
inside `ApplyExperimentalSettings`. No threading through builders. Slots
outside `1..PlayerCount` are skipped (validator warns).

## Round-trip

`SettingsFile` gains a parallel `PerPlayerOverrides` list mirroring the
runtime model. `SettingsMapper` reuses the existing uniform-bonus mapping
helper for each row. Old `.oetgs` files deserialize with the list at default
(empty) — no migration needed.

## Validation (warn-not-block)

`SettingsValidator`, gated on the starting-bonuses experimental flag:

1. `PlayerSlot < 1` or `> PlayerCount` → warn "Override targets slot N but
   template has only M players"; emitter skips.
2. Duplicate `PlayerSlot` → warn "Duplicate override for slot N; later row
   wins".
3. Override row with no fields set → warn "Override for slot N has no fields;
   remove it"; emitter ignores.

## UI

### Web (`StartingBonusesPanel.razor`)

Below the uniform controls, a "Per-player overrides" section:

- Brief explanation: "Per-slot overrides replace the uniform value for that
  field, on that slot only."
- `[+ Add override]` appends a row.
- Row: slot picker `<select>` (1..PlayerCount), then the same field controls
  used at the top, plus `[Remove]`.
- Extract uniform field controls into `StartingBonusFields.razor` so they are
  rendered identically in both contexts and stay in sync as new fields land.

### WPF (`ExperimentalPanel.xaml` Bonuses card)

Mirror Web layout: an `ItemsControl` bound to `PerPlayerOverrides` with a
DataTemplate carrying a slot ComboBox + field controls. `GatherSettings` /
`ApplySettings` round-trip the list. Watch WPF Style child ordering (Setters
before Triggers) per the recurring CI gotcha.

## Tests

Generator (`tests/OldenEra.Generator.Tests`):

1. **Defaults byte-identical**: empty `PerPlayerOverrides` → output matches a
   frozen snapshot identical to current.
2. **Single-slot override emission**: uniform `HeroAttack=2`, slot-2 row
   `HeroAttack=5`, PlayerCount=4. Assert four `Bonus` entries with
   `ReceiverSide ∈ {1,2,3,4}` and values `{2,5,2,2}`; no `ReceiverSide=-1`
   for that field.
3. **Out-of-range slot**: row with `PlayerSlot=99` is skipped.
4. **Field-level isolation**: override that sets only `HeroAttack` does not
   alter uniform `Resources` emission shape.
5. **Round-trip**: `SettingsFile` ↔ runtime preserves the list.

Web (`tests/OldenEra.Web.Tests`): bUnit smoke that `[+ Add override]` adds a
row and `[Remove]` deletes it.

WPF tests defer to windows-latest CI per existing convention.

## Build gates

`dotnet build` for `OldenEra.Generator`, `OldenEra.Web`, `OldenEra.TemplateEditor`.
`dotnet test` for `tests/OldenEra.Generator.Tests` and
`tests/OldenEra.Web.Tests`.
