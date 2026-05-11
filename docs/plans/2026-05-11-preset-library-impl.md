# Preset Library Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Bundle 3 hand-tuned `.oetgs` presets as embedded resources in `OldenEra.Generator` and expose them through a "Load preset…" entry point in both the WPF and Blazor WASM hosts.

**Architecture:** Presets are JSON files compiled as `EmbeddedResource` alongside a `presets.json` manifest. A new `PresetCatalog` service reads the manifest and loads preset payloads through the existing `SettingsFile` deserialization path. WPF integrates via a `File` submenu; Web integrates via a top-bar button + modal. Loading a preset reuses the host's existing "apply settings" code path and clears the current file path so the preset becomes a starting point rather than an editable file.

**Tech Stack:** .NET 8, C#, System.Text.Json, WPF (WPF host), Blazor WebAssembly (Web host), xUnit (tests).

**Design doc:** `docs/plans/2026-05-11-preset-library-design.md`

**Reference paths discovered during research:**
- `src/OldenEra.Generator/Models/Generator/SettingsFile.cs` — model
- `src/OldenEra.Generator/Services/SettingsMapper.cs` — `FromFile(SettingsFile)` for web
- `src/OldenEra.TemplateEditor/MainWindow.xaml.cs:851-968` — `ApplySettings(SettingsFile)` for WPF
- `src/OldenEra.TemplateEditor/MainWindow.xaml.cs:1064-1087` — existing `BtnOpen_Click`
- `src/OldenEra.TemplateEditor/MainWindow.xaml:85-92` — top-bar button strip
- `src/OldenEra.Web/Pages/Home.razor:10-37` — header/top-bar
- `src/OldenEra.Web/Pages/Home.razor:489-518` — `OnSettingsFileSelected`
- `src/OldenEra.Generator/OldenEra.Generator.csproj:14-20` — `EmbeddedResource` block
- `tests/OldenEra.Generator.Tests/SettingsFileSeedTests.cs` — xUnit conventions

---

## Task 1: PresetCatalog Service (TDD)

**Files:**
- Create: `src/OldenEra.Generator/Services/PresetCatalog.cs`
- Create: `src/OldenEra.Generator/Resources/Presets/presets.json` (with one stub entry for the test)
- Create: `src/OldenEra.Generator/Resources/Presets/_test-stub.oetgs` (minimal valid SettingsFile for the test)
- Modify: `src/OldenEra.Generator/OldenEra.Generator.csproj` (add EmbeddedResource glob)
- Test: `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs`

**Step 1: Add EmbeddedResource glob to the csproj**

In `src/OldenEra.Generator/OldenEra.Generator.csproj`, add to the existing `<ItemGroup>` containing `EmbeddedResource` entries:

```xml
<EmbeddedResource Include="Resources\Presets\*.json" />
<EmbeddedResource Include="Resources\Presets\*.oetgs" />
```

**Step 2: Create stub manifest and stub preset**

Create `src/OldenEra.Generator/Resources/Presets/presets.json`:
```json
[
  {
    "id": "_test-stub",
    "name": "Test Stub",
    "description": "Used by unit tests; not surfaced in production builds.",
    "file": "_test-stub.oetgs"
  }
]
```

Create `src/OldenEra.Generator/Resources/Presets/_test-stub.oetgs` with a minimal valid `SettingsFile` payload:
```json
{
  "templateName": "Test Stub",
  "seed": 42
}
```

(All other `SettingsFile` properties get default values during deserialization, so this is sufficient.)

**Step 3: Write the failing test**

Create `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs`:

```csharp
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class PresetCatalogTests
{
    [Fact]
    public void Entries_AreReadFromEmbeddedManifest()
    {
        var catalog = new PresetCatalog();

        Assert.NotEmpty(catalog.Entries);
        Assert.Contains(catalog.Entries, e => e.Id == "_test-stub");
    }

    [Fact]
    public void Load_ReturnsDeserializedSettingsFile()
    {
        var catalog = new PresetCatalog();

        var settings = catalog.Load("_test-stub");

        Assert.NotNull(settings);
        Assert.Equal("Test Stub", settings.TemplateName);
        Assert.Equal(42, settings.Seed);
    }

    [Fact]
    public void Load_ThrowsForUnknownId()
    {
        var catalog = new PresetCatalog();

        Assert.Throws<KeyNotFoundException>(() => catalog.Load("does-not-exist"));
    }

    [Fact]
    public void EveryManifestEntry_LoadsSuccessfully()
    {
        var catalog = new PresetCatalog();

        foreach (var entry in catalog.Entries)
        {
            var settings = catalog.Load(entry.Id);
            Assert.NotNull(settings);
        }
    }
}
```

**Step 4: Run the test to verify it fails**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter PresetCatalogTests`
Expected: FAIL — `PresetCatalog` and `PresetEntry` types do not exist.

**Step 5: Implement PresetCatalog**

Create `src/OldenEra.Generator/Services/PresetCatalog.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models.Generator;

namespace OldenEra.Generator.Services;

public sealed record PresetEntry(string Id, string Name, string Description, string File);

public sealed class PresetCatalog
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions SettingsOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ManifestResource = "OldenEra.Generator.Resources.Presets.presets.json";
    private const string PresetResourcePrefix = "OldenEra.Generator.Resources.Presets.";

    private readonly Assembly _assembly;
    private readonly Dictionary<string, PresetEntry> _byId;

    public IReadOnlyList<PresetEntry> Entries { get; }

    public PresetCatalog() : this(typeof(PresetCatalog).Assembly) { }

    internal PresetCatalog(Assembly assembly)
    {
        _assembly = assembly;
        using var stream = _assembly.GetManifestResourceStream(ManifestResource)
            ?? throw new InvalidOperationException(
                $"Embedded manifest '{ManifestResource}' was not found. " +
                $"Check that Resources/Presets/presets.json is included as EmbeddedResource.");

        var entries = JsonSerializer.Deserialize<List<PresetEntry>>(stream, ManifestOptions)
            ?? new List<PresetEntry>();

        Entries = entries;
        _byId = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    public SettingsFile Load(string id)
    {
        if (!_byId.TryGetValue(id, out var entry))
            throw new KeyNotFoundException($"No preset with id '{id}'.");

        var resourceName = PresetResourcePrefix + entry.File;
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded preset '{resourceName}' was not found.");

        var settings = JsonSerializer.Deserialize<SettingsFile>(stream, SettingsOptions)
            ?? throw new InvalidDataException($"Preset '{id}' deserialized to null.");

        return settings;
    }
}
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter PresetCatalogTests`
Expected: PASS — all 4 tests green.

**Step 7: Commit**

```bash
git add src/OldenEra.Generator/Services/PresetCatalog.cs \
        src/OldenEra.Generator/Resources/Presets/ \
        src/OldenEra.Generator/OldenEra.Generator.csproj \
        tests/OldenEra.Generator.Tests/PresetCatalogTests.cs
git commit -m "feat: PresetCatalog service for embedded .oetgs presets (#25)"
```

---

## Task 2: Author the v1 Presets

**Files:**
- Create: `src/OldenEra.Generator/Resources/Presets/jebus-like.oetgs`
- Create: `src/OldenEra.Generator/Resources/Presets/arcade-2v2.oetgs`
- Create: `src/OldenEra.Generator/Resources/Presets/big-map-ffa.oetgs`
- Modify: `src/OldenEra.Generator/Resources/Presets/presets.json`
- Delete: `src/OldenEra.Generator/Resources/Presets/_test-stub.oetgs`
- Modify: `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs` (drop the `_test-stub` assertion, replace with real ids)

**Authoring approach:**

Each preset is a `SettingsFile` JSON. Look at `src/OldenEra.Generator/Models/Generator/SettingsFile.cs` for the property names and defaults. Author each preset by writing the JSON directly (not by exporting from the editor — the editor isn't reliably runnable on Mac). Set only the fields that differ from defaults; the rest deserialize to their default values.

**Step 1: Author `jebus-like.oetgs`**

```json
{
  "templateName": "Jebus-like",
  "playerCount": 2,
  "topology": "Ring",
  "mapSize": "Medium",
  "neutralZoneCount": 1,
  "playerZoneCastles": 1,
  "victoryCondition": "HoldCity",
  "treasureDensity": "High"
}
```

(Verify exact property names and enum string values against `SettingsFile.cs` before writing — the names above are the design intent; the actual property names may differ slightly. Use whatever names appear in the model with `[JsonPropertyName(...)]`.)

**Step 2: Author `arcade-2v2.oetgs`**

```json
{
  "templateName": "Arcade 2v2",
  "playerCount": 4,
  "topology": "Chain",
  "mapSize": "Small",
  "neutralZoneCount": 0,
  "playerZoneCastles": 1
}
```

**Step 3: Author `big-map-ffa.oetgs`**

```json
{
  "templateName": "Big Map FFA",
  "playerCount": 8,
  "topology": "Random",
  "mapSize": "ExtraLarge",
  "neutralZoneCount": 6,
  "treasureDensity": "High"
}
```

**Step 4: Update the manifest**

Replace `src/OldenEra.Generator/Resources/Presets/presets.json` with:

```json
[
  {
    "id": "jebus-like",
    "name": "Jebus-like",
    "description": "2 player ring map, treasure-rich center, hold-city win.",
    "file": "jebus-like.oetgs"
  },
  {
    "id": "arcade-2v2",
    "name": "Arcade 2v2",
    "description": "4 player chain, fast pacing, low neutral count.",
    "file": "arcade-2v2.oetgs"
  },
  {
    "id": "big-map-ffa",
    "name": "Big Map FFA",
    "description": "8 player random topology, large map, high neutral density.",
    "file": "big-map-ffa.oetgs"
  }
]
```

**Step 5: Remove the test stub and update the test**

Delete `src/OldenEra.Generator/Resources/Presets/_test-stub.oetgs`.

In `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs`, change the first two tests to use real ids:

```csharp
[Fact]
public void Entries_AreReadFromEmbeddedManifest()
{
    var catalog = new PresetCatalog();
    Assert.Equal(3, catalog.Entries.Count);
    Assert.Contains(catalog.Entries, e => e.Id == "jebus-like");
    Assert.Contains(catalog.Entries, e => e.Id == "arcade-2v2");
    Assert.Contains(catalog.Entries, e => e.Id == "big-map-ffa");
}

[Fact]
public void Load_ReturnsDeserializedSettingsFile()
{
    var catalog = new PresetCatalog();
    var settings = catalog.Load("jebus-like");
    Assert.NotNull(settings);
    Assert.Equal("Jebus-like", settings.TemplateName);
}
```

The `EveryManifestEntry_LoadsSuccessfully` and `Load_ThrowsForUnknownId` tests remain unchanged.

**Step 6: Run the tests**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter PresetCatalogTests`
Expected: PASS — all 4 tests green, all 3 presets load.

**Step 7: Commit**

```bash
git add src/OldenEra.Generator/Resources/Presets/ \
        tests/OldenEra.Generator.Tests/PresetCatalogTests.cs
git commit -m "feat: author v1 preset library (Jebus-like, Arcade 2v2, Big Map FFA)"
```

---

## Task 3: WPF "Load preset…" Submenu

**Files:**
- Modify: `src/OldenEra.TemplateEditor/MainWindow.xaml` (add menu near `Open…` button)
- Modify: `src/OldenEra.TemplateEditor/MainWindow.xaml.cs` (handler + catalog field)

**Note on the WPF top bar:** `MainWindow.xaml:85-92` currently uses simple `<Button>` elements, not a `Menu`. The cleanest addition is a `Menu` host with one `MenuItem` whose dropdown lists the presets. Alternative: a button with a `ContextMenu`. Use the `Menu` approach for visibility.

**Step 1: Add the menu to the top-bar StackPanel**

In `src/OldenEra.TemplateEditor/MainWindow.xaml`, inside the existing top-bar `<StackPanel DockPanel.Dock="Left" Orientation="Horizontal">` near line 85, add after the `Open…` button:

```xaml
<Menu Background="Transparent" VerticalAlignment="Center">
    <MenuItem Header="Load preset…" x:Name="MnuLoadPreset" SubmenuOpened="MnuLoadPreset_SubmenuOpened" />
</Menu>
```

(Match indentation and any styling conventions of nearby buttons. If a styled menu doesn't exist in the dark theme yet, accept default styling for now — visual polish is out of scope.)

**Step 2: Add catalog field and handler in code-behind**

In `src/OldenEra.TemplateEditor/MainWindow.xaml.cs`, add a field near the other private fields (around line 34):

```csharp
private readonly PresetCatalog _presetCatalog = new();
```

Add `using OldenEra.Generator.Services;` to the top of the file if it's not already there.

Add this handler near the other menu handlers (e.g., next to `BtnOpen_Click`):

```csharp
private void MnuLoadPreset_SubmenuOpened(object sender, RoutedEventArgs e)
{
    if (MnuLoadPreset.Items.Count > 0) return;

    foreach (var entry in _presetCatalog.Entries)
    {
        var item = new MenuItem
        {
            Header = entry.Name,
            ToolTip = entry.Description,
            Tag = entry.Id,
        };
        item.Click += MnuPresetEntry_Click;
        MnuLoadPreset.Items.Add(item);
    }
}

private void MnuPresetEntry_Click(object sender, RoutedEventArgs e)
{
    if (sender is not MenuItem item || item.Tag is not string id) return;

    try
    {
        var settings = _presetCatalog.Load(id);
        ApplySettings(settings);
        _currentSettingsPath = null;
        _isDirty = true;
        UpdateTitle();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Failed to load preset:\n{ex.Message}", "Preset Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

**Step 3: Build the WPF project to verify it compiles**

Run: `dotnet build src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj`

Note: WPF builds may not run on macOS (Windows-only target). If the build fails on macOS with a platform error rather than a code error, that's expected — flag this for the user to verify on Windows or in CI.

Expected (Windows/CI): build succeeds with no errors.

**Step 4: Commit**

```bash
git add src/OldenEra.TemplateEditor/MainWindow.xaml \
        src/OldenEra.TemplateEditor/MainWindow.xaml.cs
git commit -m "feat(wpf): add Load preset submenu (#25)"
```

---

## Task 4: Web "Load preset…" Modal

**Files:**
- Create: `src/OldenEra.Web/Components/PresetPickerModal.razor` (new component)
- Modify: `src/OldenEra.Web/Pages/Home.razor` (add button to header + modal instance + handler)
- Modify: `src/OldenEra.Web/wwwroot/css/site.css` or a co-located CSS file (modal styling)
- Modify: `src/OldenEra.Web/Program.cs` (register `PresetCatalog` as singleton, if DI is in use)

**Step 1: Register PresetCatalog with DI**

In `src/OldenEra.Web/Program.cs`, add the service registration alongside the existing service registrations:

```csharp
builder.Services.AddSingleton<PresetCatalog>();
```

(Add `using OldenEra.Generator.Services;` if needed.)

**Step 2: Create the modal component**

Create `src/OldenEra.Web/Components/PresetPickerModal.razor`:

```razor
@using OldenEra.Generator.Services
@inject PresetCatalog Catalog

@if (IsOpen)
{
    <div class="oe-modal-backdrop" @onclick="Close">
        <div class="oe-modal" @onclick:stopPropagation="true">
            <h2>Load preset</h2>
            <p class="oe-modal-hint">Pick a starting point. Your current settings will be replaced.</p>
            <ul class="oe-preset-list">
                @foreach (var entry in Catalog.Entries)
                {
                    <li>
                        <button class="oe-preset-item" @onclick="() => Pick(entry.Id)">
                            <span class="oe-preset-name">@entry.Name</span>
                            <span class="oe-preset-desc">@entry.Description</span>
                        </button>
                    </li>
                }
            </ul>
            <div class="oe-modal-actions">
                <button class="oe-btn ghost" @onclick="Close">Cancel</button>
            </div>
        </div>
    </div>
}

@code {
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback<string> OnPick { get; set; }

    private async Task Close()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    private async Task Pick(string id)
    {
        await OnPick.InvokeAsync(id);
        await Close();
    }
}
```

**Step 3: Add minimal CSS**

Append to `src/OldenEra.Web/wwwroot/css/site.css` (or whatever the host CSS file is — check for an existing `.oe-modal` rule first; if the project already has a modal pattern, reuse it):

```css
.oe-modal-backdrop {
    position: fixed; inset: 0;
    background: rgba(0,0,0,0.55);
    display: flex; align-items: center; justify-content: center;
    z-index: 1000;
}
.oe-modal {
    background: var(--oe-panel, #1e1e1e);
    color: var(--oe-fg, #eee);
    border: 1px solid var(--oe-border, #444);
    border-radius: 6px;
    padding: 1.25rem 1.5rem;
    min-width: 380px;
    max-width: 560px;
    box-shadow: 0 10px 40px rgba(0,0,0,0.4);
}
.oe-modal h2 { margin: 0 0 0.5rem 0; font-size: 1.1rem; }
.oe-modal-hint { margin: 0 0 1rem 0; opacity: 0.75; font-size: 0.9rem; }
.oe-preset-list { list-style: none; padding: 0; margin: 0 0 1rem 0; }
.oe-preset-list li { margin-bottom: 0.5rem; }
.oe-preset-item {
    display: flex; flex-direction: column; align-items: flex-start;
    width: 100%; padding: 0.6rem 0.8rem;
    background: var(--oe-panel-2, #2a2a2a);
    color: inherit;
    border: 1px solid var(--oe-border, #444);
    border-radius: 4px;
    cursor: pointer;
    text-align: left;
}
.oe-preset-item:hover { background: var(--oe-panel-hover, #333); }
.oe-preset-name { font-weight: 600; }
.oe-preset-desc { font-size: 0.85rem; opacity: 0.8; margin-top: 0.2rem; }
.oe-modal-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }
```

(If the project uses CSS variables that differ from `--oe-panel` etc., substitute the actual variable names. Check `site.css` for the existing palette before committing.)

**Step 4: Wire up the button and modal in Home.razor**

In `src/OldenEra.Web/Pages/Home.razor`:

(a) Add an `@inject` line near the top with the other injects:

```razor
@inject OldenEra.Generator.Services.PresetCatalog PresetCatalog
```

(b) In the header `<div class="oe-header-actions">` (around line 17), add a button just before `Open settings…`:

```razor
<button class="oe-btn ghost" @onclick="OpenPresetPicker">Load preset…</button>
```

(c) Near the existing `<InputFile>` element (around line 34), add the modal instance:

```razor
<PresetPickerModal @bind-IsOpen="IsPresetPickerOpen" OnPick="OnPresetPicked" />
```

(d) Add the supporting state and methods to the `@code` block:

```csharp
private bool IsPresetPickerOpen;

private void OpenPresetPicker() => IsPresetPickerOpen = true;

private async Task OnPresetPicked(string id)
{
    try
    {
        var s = PresetCatalog.Load(id);
        var (mapped, advanced, experimental, expEnabled) = SettingsMapper.FromFile(s);
        Settings = mapped;
        AdvancedMode = advanced;
        ExperimentalMapSizes = experimental;
        ExperimentalEnabled = expEnabled;
        Validate();
        await SettingsStore.SaveAsync(SettingsMapper.ToFile(Settings, AdvancedMode, ExperimentalMapSizes, ExperimentalEnabled));
        StateHasChanged();
    }
    catch (Exception ex)
    {
        ValidationMessage = $"Failed to load preset: {ex.Message}";
        IsValid = false;
        StateHasChanged();
    }
}
```

This mirrors `OnSettingsFileSelected` (Home.razor:489-518) but skips the file stream parsing — `PresetCatalog.Load` returns the `SettingsFile` directly.

**Step 5: Build the web project**

Run: `dotnet build src/OldenEra.Web/OldenEra.Web.csproj`
Expected: build succeeds.

**Step 6: Manually smoke test in dev server**

Run: `dotnet watch --project src/OldenEra.Web run` (per the project memory: always `watch`, never `run`).

In a browser:
1. Click "Load preset…" — modal appears.
2. Click "Jebus-like" — modal closes, settings populate, validation passes.
3. Click "Load preset…" again, click backdrop — modal closes without changes.
4. Click "Load preset…", pick "Big Map FFA" — settings update.

If the dev server / browser test isn't possible, note that explicitly in the task report.

**Step 7: Commit**

```bash
git add src/OldenEra.Web/Components/PresetPickerModal.razor \
        src/OldenEra.Web/Pages/Home.razor \
        src/OldenEra.Web/Program.cs \
        src/OldenEra.Web/wwwroot/css/site.css
git commit -m "feat(web): add Load preset modal (#25)"
```

---

## Task 5: Round-trip Test

**Files:**
- Modify: `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs`

**Step 1: Add a round-trip test**

Append to `PresetCatalogTests`:

```csharp
[Fact]
public void EveryPreset_RoundTripsThroughJson()
{
    var catalog = new PresetCatalog();
    var opts = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    foreach (var entry in catalog.Entries)
    {
        var loaded = catalog.Load(entry.Id);
        var json = JsonSerializer.Serialize(loaded, opts);
        var roundTripped = JsonSerializer.Deserialize<SettingsFile>(json, opts);

        Assert.NotNull(roundTripped);
        Assert.Equal(loaded.TemplateName, roundTripped!.TemplateName);
        Assert.Equal(loaded.Seed, roundTripped.Seed);
    }
}
```

Add the necessary `using` statements at the top of the test file if missing:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models.Generator;
```

**Step 2: Run tests**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter PresetCatalogTests`
Expected: PASS — 5 tests green.

**Step 3: Commit**

```bash
git add tests/OldenEra.Generator.Tests/PresetCatalogTests.cs
git commit -m "test: round-trip presets through SettingsFile JSON"
```

---

## Task 6: Final Verification

**Step 1: Run the full test suite**

Run: `dotnet test`
Expected: all tests pass; no regressions in existing suites.

**Step 2: Verify the design and impl docs are committed**

Run: `git log --oneline main..HEAD`
Expected: commits for design doc, PresetCatalog, presets, WPF integration, web integration, round-trip test, and impl plan.

**Step 3: Cross-check against acceptance criteria from issue #25**

- [ ] Loading a preset populates settings identically to opening that `.oetgs` from disk — confirmed by reusing `ApplySettings` (WPF) / `SettingsMapper.FromFile` (Web).
- [ ] Presets are read-only (loading marks state as dirty/untitled, not "open file path") — confirmed: `_currentSettingsPath = null; _isDirty = true;` in WPF; web saves to localStorage but no file-path concept exists.
- [ ] New presets can be added without code changes — confirmed: drop a `.oetgs` into `Resources/Presets/` and add a manifest entry.

**Step 4: If everything is green, ready for code review and PR**

Use `superpowers:requesting-code-review` skill, then `superpowers:finishing-a-development-branch`.

---

## Notes for the Executing Engineer

- **macOS WPF build limit:** `OldenEra.TemplateEditor` may not build cleanly on Mac. If the WPF build step fails with `Microsoft.WindowsDesktop.App` errors, that's a platform issue, not a code issue — push and let CI verify.
- **Preset settings authoring:** The JSON values in Task 2 are *intent-level*. Before authoring, open `SettingsFile.cs` and confirm the actual `[JsonPropertyName(...)]` keys and the type (string/int/enum) of each field. Use only fields that exist; the design's archetypes are the goal, not the literal field names listed here.
- **JSON casing:** The codebase uses lowercase camelCase keys for `.oetgs` (e.g., `templateName`). The `presets.json` manifest uses the same convention.
- **Frequent commits:** every task ends with a commit. Don't squash before PR — the commits map to the task structure and aid review.
