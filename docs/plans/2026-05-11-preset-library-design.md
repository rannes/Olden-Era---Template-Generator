# Preset Library Design — Bundle `.oetgs` Templates

**Issue:** [#25 — Bundle preset library](https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/issues/25)
**Date:** 2026-05-11
**Status:** Design approved, ready for implementation plan

## Problem

First-time users land on a blank settings panel with 100+ knobs and no curated
starting points. We want to ship a small set of hand-tuned `.oetgs` presets and
make them discoverable in both the WPF and Blazor WASM hosts.

## Design Summary

- Presets are `.oetgs` files embedded in `OldenEra.Generator.dll` alongside a
  `presets.json` manifest.
- A new `PresetCatalog` service reads the manifest and loads preset payloads.
- Each host adds a symmetric "Load preset…" entry point: WPF as a `File`
  submenu, Web as a top-bar button that opens a modal.
- Loading a preset applies its settings, clears the current file path, and
  marks state dirty — identical to "New from template."
- v1 ships three presets; two more are deferred.

## Storage and Discovery

### Layout

```
src/OldenEra.Generator/
  Presets/
    presets.json
    jebus-like.oetgs
    arcade-2v2.oetgs
    big-map-ffa.oetgs
```

All four files are marked as `EmbeddedResource` in
`OldenEra.Generator.csproj`. This keeps the same code path working in WPF
(filesystem available) and Blazor WASM (no folder enumeration).

### Manifest format

```json
[
  {
    "id": "jebus-like",
    "name": "Jebus-like",
    "description": "2 player ring map, treasure-rich center, hold-city win.",
    "file": "jebus-like.oetgs"
  },
  ...
]
```

`id` is the stable key used by the picker. `file` is the embedded resource
name. Adding a preset means dropping a `.oetgs` plus appending a manifest
entry — no C# changes.

## PresetCatalog Service

New file: `src/OldenEra.Generator/Services/PresetCatalog.cs`.

```csharp
public sealed record PresetEntry(string Id, string Name, string Description);

public sealed class PresetCatalog
{
    public IReadOnlyList<PresetEntry> Entries { get; }
    public SettingsFile Load(string id);
}
```

- Constructor reads `presets.json` from the assembly's embedded resources and
  parses it into `Entries`.
- `Load(id)` reads the corresponding `.oetgs` resource stream and deserializes
  it via the existing `SettingsFile` JSON pipeline.
- Throws `KeyNotFoundException` if `id` is not in the manifest.
- No filesystem access, no host-specific code.

## Host Integration

### WPF

`MainWindow.xaml`: add a `File → Load preset…` submenu. Items are bound to
`PresetCatalog.Entries`; each `MenuItem` shows `Name` with `Description` as
its `ToolTip`.

Click handler:
1. Call `PresetCatalog.Load(entry.Id)` → `SettingsFile`.
2. Apply via the same path used by `Open` (sets all VM state).
3. Clear current file path, mark dirty.

### Web

`Layout/MainLayout.razor` (or top-bar component): add a "Load preset…"
button. Click opens a small modal listing entries (name + description). Each
entry is a button; clicking it:

1. Calls `PresetCatalog.Load(entry.Id)`.
2. Routes through the same apply-settings path used by drag-and-drop / open.
3. Clears any "loaded from URL/file" state, marks dirty.

The modal closes on selection or backdrop click. No empty-state hint in v1 —
the top-bar button is always visible.

## Loaded State Semantics

After applying a preset:

- Settings VM is fully overwritten.
- "Current file path" is cleared (WPF: title shows `Untitled*`; Web:
  equivalent state).
- Dirty flag set to `true`.
- `Save` prompts for filename; `Save As` is the only persistence path.

This matches the acceptance criterion exactly and avoids any "am I editing the
preset itself?" ambiguity.

## v1 Preset Contents

Three presets, chosen for archetype distinctness:

1. **Jebus-like** — 2 player, ring topology, treasure-rich center, hold-city
   win condition.
2. **Arcade 2v2** — 4 player chain or hub-and-spoke, fast pacing, low neutral
   count.
3. **Big Map FFA** — 6–8 player random topology, large map, high neutral
   density.

**Deferred:**

- *Single Hero Duel* — blocked on the `GameMode` picker issue.
- *Tournament Mirror* — overlaps heavily with Jebus-like; revisit after user
  feedback.

Each preset author hand-tunes the `.oetgs` by exporting from the editor,
manually verifying the resulting settings, and committing the file plus a
manifest entry.

## Testing

- **PresetCatalog unit tests:** manifest loads, `Entries` count matches
  manifest, `Load(id)` returns a non-null `SettingsFile` for each entry,
  unknown id throws.
- **Round-trip:** loading each preset and saving it produces an equivalent
  `SettingsFile` (no parse loss).
- **Hosts:** smoke-tested manually — automated UI tests are out of scope for
  this change.

## Out of Scope

- "Recent .oetgs" list (mentioned in the issue but a separate concern).
- Preset screenshots / thumbnails.
- User-defined presets.
- Empty-state hint on web (can layer on later).
