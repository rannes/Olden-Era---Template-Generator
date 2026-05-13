# Zone Content — Round 5 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close out the customizable zone-content feature by porting the Round 4 Web editor to WPF and adding Sid autocomplete plus category-grouped preset picker on both hosts.

**Architecture:** Two threads. Thread B lands on `main` first: relocates three host-shared helpers from `OldenEra.Web` into `OldenEra.Generator`, adds a `Category` field to `ZoneContentPreset`, wires `ZoneContentSidCatalog` into the Web Sid input as a `<datalist>`, and groups the Web preset picker via `<optgroup>`. Thread A lands on a worktree `feature/zone-content-round-5`: ports the Round 4 zone-content editor to WPF using INPC view-model wrappers, with VM unit tests in a new `OldenEra.TemplateEditor.Tests` project that builds on Mac (no WPF assembly references).

**Tech Stack:** .NET 10, C#, Blazor WASM (Web), WPF (Desktop), xUnit, FluentAssertions.

**Reference:** Companion design doc — `docs/plans/2026-05-13-zone-content-round-5-design.md`.

**Workflow:**
- Mac dev only. Generator + Web + tests build on Mac. WPF does NOT build on Mac — every WPF change is a CI round-trip.
- TDD-sliced commits. Aim for ~15 commits across both threads.
- PRs target the fork: `gh pr create --repo rannes/Olden-Era---Template-Generator`.
- Style.Triggers must come after Setters (memory: `feedback_wpf_style_child_order`).

---

## Thread B — On `main`

### Task B1: Relocate `ZoneContentCloning` to `OldenEra.Generator`

**Files:**
- Move: `src/OldenEra.Web/Services/ZoneContentCloning.cs` → `src/OldenEra.Generator/Services/ZoneContent/ZoneContentCloning.cs`
- Move: `tests/OldenEra.Web.Tests/Services/ZoneContentCloningTests.cs` → `tests/OldenEra.Generator.Tests/Services/ZoneContent/ZoneContentCloningTests.cs`
- Modify (namespace `using` lines): every Razor file in `src/OldenEra.Web/Components/ZoneContent/` and any Web test file referencing `OldenEra.Web.Services.ZoneContentCloning`.

**Step 1: Move source file**

Use `git mv src/OldenEra.Web/Services/ZoneContentCloning.cs src/OldenEra.Generator/Services/ZoneContent/ZoneContentCloning.cs`.

**Step 2: Update namespace in moved file**

Change `namespace OldenEra.Web.Services` to `namespace OldenEra.Generator.Services.ZoneContent`.

**Step 3: Move test file**

Use `git mv tests/OldenEra.Web.Tests/Services/ZoneContentCloningTests.cs tests/OldenEra.Generator.Tests/Services/ZoneContent/ZoneContentCloningTests.cs`. Update namespace and any class-level `using` lines.

**Step 4: Update consumer `using` directives**

Run `grep -rln "OldenEra.Web.Services.ZoneContentCloning\|using OldenEra.Web.Services;.*ZoneContentCloning" src/OldenEra.Web tests/OldenEra.Web.Tests`. For each hit, ensure the file has `@using OldenEra.Generator.Services.ZoneContent` (Razor) or `using OldenEra.Generator.Services.ZoneContent;` (C#) — keep the `OldenEra.Web.Services` import only if other types in that namespace are still consumed.

**Step 5: Verify**

Run `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj`.
Run `dotnet build src/OldenEra.Web/OldenEra.Web.csproj`.
Run `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`.
Run `dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj`.
All four expected to pass.

**Step 6: Commit**

```bash
git add -A
git commit -m "refactor(library): relocate ZoneContentCloning to OldenEra.Generator"
```

---

### Task B2: Relocate `ZoneContentScope`

**Files:**
- Move: `src/OldenEra.Web/Services/ZoneContentScope.cs` → `src/OldenEra.Generator/Services/ZoneContent/ZoneContentScope.cs`
- Move associated tests if present in `tests/OldenEra.Web.Tests/Services/`.

**Step 1: Move source file with `git mv` and update namespace.**

**Step 2: Move any matching test file with `git mv` and update namespace.**

**Step 3: Update consumer `using` directives.** Same `grep` pattern as B1.

**Step 4: Verify with the four build/test commands from B1.**

**Step 5: Commit**

```bash
git commit -m "refactor(library): relocate ZoneContentScope to OldenEra.Generator"
```

---

### Task B3: Relocate `ZoneContentWarningProjection`

Same pattern as B2.

**Step 1: `git mv` source file, update namespace.**
**Step 2: `git mv` matching test file (if present), update namespace.**
**Step 3: Update consumer `using` directives.**
**Step 4: Verify.**
**Step 5: Commit:**

```bash
git commit -m "refactor(library): relocate ZoneContentWarningProjection to OldenEra.Generator"
```

---

### Task B4: Add `Category` field to `ZoneContentPreset` (TDD)

**Files:**
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentPresets.cs`
- Test: `tests/OldenEra.Generator.Tests/Services/ZoneContent/ZoneContentPresetsTests.cs` (create or extend)

**Step 1: Write failing test**

```csharp
[Fact]
public void All_presets_have_non_empty_category()
{
    foreach (var preset in ZoneContentPresets.All())
    {
        preset.Category.Should().NotBeNullOrWhiteSpace();
    }
}
```

**Step 2: Run test, expect FAIL** (compile error: `Category` doesn't exist).

`dotnet test tests/OldenEra.Generator.Tests --filter All_presets_have_non_empty_category`

**Step 3: Implement**

Change record to `public sealed record ZoneContentPreset(string Name, string Category, ZoneContentItem Item);`. Update the four existing entries in `All()` to insert `"Mandatory"` as the second positional argument.

**Step 4: Run test, expect PASS.**

**Step 5: Verify no regressions.** `dotnet test tests/OldenEra.Generator.Tests`. `dotnet test tests/OldenEra.Web.Tests` (Web preset-picker tests may need the new positional arg if they construct `ZoneContentPreset` directly — fix and re-run).

**Step 6: Commit**

```bash
git commit -m "feat(library): add Category to ZoneContentPreset"
```

---

### Task B5: Web — wire `ZoneContentSidCatalog` into Sid input as `<datalist>`

**Files:**
- Modify: the Razor component(s) in `src/OldenEra.Web/Components/ZoneContent/` that render the Sid `<input>`. Locate via `grep -rln 'name="sid"\|@bind="@.*\.Sid"' src/OldenEra.Web/Components/ZoneContent/`.
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentSidCatalog.cs` (comment update).

**Step 1: Update catalog comment**

Replace the "A follow-up will union this seed…" sentence with: "Catalog expansion (e.g., friendly-name source for `GameDataCatalog` / `CommunityCatalog`) is deferred to a future round; the seed intentionally stays small."

**Step 2: Render `<datalist>` in the editor component**

Once per editor (not per row), render:
```razor
<datalist id="zone-content-sids">
    @foreach (var entry in ZoneContentSidCatalog.All())
    {
        <option value="@entry.Sid" label="@entry.FriendlyName"></option>
    }
</datalist>
```

**Step 3: Bind input to datalist**

Add `list="zone-content-sids"` to the existing Sid `<input>` element.

**Step 4: Verify in browser**

Run `dotnet watch run --project src/OldenEra.Web/OldenEra.Web.csproj`. Open the page, focus a Sid input, confirm the four friendly-named options appear. Confirm typing still works for arbitrary values.

**Step 5: Commit**

```bash
git commit -m "feat(web): wire Sid catalog into datalist autocomplete"
```

---

### Task B6: Web — group preset picker via `<optgroup>`

**Files:**
- Modify: the Razor component in `src/OldenEra.Web/Components/ZoneContent/` that renders the "+ Add from preset…" `<select>`. Locate via `grep -rln 'Add from preset' src/OldenEra.Web/Components/ZoneContent/`.

**Step 1: Wrap options by category**

Replace flat `@foreach (var p in ZoneContentPresets.All())` with:
```razor
@foreach (var group in ZoneContentPresets.All().GroupBy(p => p.Category))
{
    <optgroup label="@group.Key">
        @foreach (var p in group)
        {
            <option value="@p.Name">@p.Name</option>
        }
    </optgroup>
}
```

**Step 2: Verify in browser**

`dotnet watch run`, exercise the preset picker. With four entries all in "Mandatory", the dropdown shows one optgroup. The structural change is the deliverable.

**Step 3: Commit**

```bash
git commit -m "feat(web): group zone-content preset picker by category"
```

---

### Task B7: Open Thread B PR

**Step 1: Run all Mac-buildable tests**

```bash
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj
```
Both expected to pass.

**Step 2: Push and open PR**

```bash
git push -u origin main
gh pr create --repo rannes/Olden-Era---Template-Generator --base main --title "feat: zone-content round 5 — thread B (helpers + autocomplete)" --body "$(cat <<'EOF'
## Summary
- Relocates ZoneContentCloning, ZoneContentScope, ZoneContentWarningProjection from OldenEra.Web to OldenEra.Generator so both hosts share them.
- Adds Category field to ZoneContentPreset; backfills the four current presets as "Mandatory".
- Wires ZoneContentSidCatalog into the Web Sid input as a <datalist> for friendly-name autocomplete.
- Groups the Web preset picker by category via <optgroup> (designed to scale to 20+ presets).

## Test plan
- [x] `dotnet test` — Generator and Web test projects pass.
- [x] Manual browser verification of Sid datalist and preset picker grouping.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

(Note: this branch is `main` in the fork; pushing to `main` is acceptable per repo conventions, but verify the PR base is the fork's main, not upstream KhanDevelopsGames.)

**Step 3: Watch CI, merge when green.**

---

## Thread A — Worktree `feature/zone-content-round-5`

**Setup before Task A1:** Use `superpowers:using-git-worktrees` to create
`.worktrees/zone-content-round-5` off `main` (after Thread B is merged).

### Task A1: Create `OldenEra.TemplateEditor.Tests` project

**Files:**
- Create: `tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj`
- Create: `tests/OldenEra.TemplateEditor.Tests/Placeholder.cs` (one trivial passing test to prove the project builds on Mac).

**Step 1: Create csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="*" />
    <PackageReference Include="xunit" Version="*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="*" />
    <PackageReference Include="FluentAssertions" Version="*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\OldenEra.Generator\OldenEra.Generator.csproj" />
  </ItemGroup>
  <ItemGroup>
    <!-- Reference VM source files directly; do NOT reference OldenEra.TemplateEditor.csproj
         because that pulls in WPF assemblies that don't build on Mac. -->
    <Compile Include="..\..\src\OldenEra.TemplateEditor\ViewModels\*.cs" LinkBase="ViewModels" />
  </ItemGroup>
</Project>
```

(Match the package reference versions to the existing test projects' csproj — read `tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` to copy.)

**Step 2: Add a placeholder test**

```csharp
namespace OldenEra.TemplateEditor.Tests;
public class PlaceholderTests
{
    [Fact] public void Project_builds() => Assert.True(true);
}
```

**Step 3: Add to `OldenEra.slnx`** (do NOT try to build the slnx on Mac — just edit it).

**Step 4: Verify on Mac**

`dotnet test tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj`. Expected PASS.

**Step 5: Commit + push** (CI verifies the slnx still builds on Windows).

```bash
git commit -m "chore(tests): scaffold OldenEra.TemplateEditor.Tests project"
git push -u origin feature/zone-content-round-5
```

Watch CI before proceeding.

---

### Task A2: `ZoneContentItemViewModel` (TDD)

**Files:**
- Create: `src/OldenEra.TemplateEditor/ViewModels/ZoneContentItemViewModel.cs`
- Create: `tests/OldenEra.TemplateEditor.Tests/ViewModels/ZoneContentItemViewModelTests.cs`

**Step 1: Write failing tests**

Cover:
- `FromModel` populates all properties.
- `ToModel` produces an equivalent `ZoneContentItem`.
- `FactionAffinityCsv` round-trips: empty → empty list, `"a, b , c"` → `["a","b","c"]`, list `["x","y"]` → `"x, y"`.
- Same shape for `BiomeFilterCsv`.
- Setting `MinCount` raises `PropertyChanged` for `MinCount`.

**Step 2: Run, expect FAIL** (compile error).

**Step 3: Implement minimal VM**

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using OldenEra.Generator.Models;

namespace OldenEra.TemplateEditor.ViewModels;

public sealed class ZoneContentItemViewModel : INotifyPropertyChanged
{
    private string _sid = "";
    private int _minCount;
    private int _maxCount;
    private ZoneContentPool _pool;
    private bool _isGuarded;
    private bool _nearCastle;
    private bool _isRoadDecoration;
    private string _factionAffinityCsv = "";
    private string _biomeFilterCsv = "";

    public string Sid { get => _sid; set => Set(ref _sid, value); }
    public int MinCount { get => _minCount; set => Set(ref _minCount, value); }
    public int MaxCount { get => _maxCount; set => Set(ref _maxCount, value); }
    public ZoneContentPool Pool { get => _pool; set => Set(ref _pool, value); }
    public bool IsGuarded { get => _isGuarded; set => Set(ref _isGuarded, value); }
    public bool NearCastle { get => _nearCastle; set => Set(ref _nearCastle, value); }
    public bool IsRoadDecoration { get => _isRoadDecoration; set => Set(ref _isRoadDecoration, value); }
    public string FactionAffinityCsv { get => _factionAffinityCsv; set => Set(ref _factionAffinityCsv, value); }
    public string BiomeFilterCsv { get => _biomeFilterCsv; set => Set(ref _biomeFilterCsv, value); }

    public static ZoneContentItemViewModel FromModel(ZoneContentItem m) => new()
    {
        Sid = m.Sid ?? "",
        MinCount = m.MinCount,
        MaxCount = m.MaxCount,
        Pool = m.Pool,
        IsGuarded = m.IsGuarded,
        NearCastle = m.NearCastle,
        IsRoadDecoration = m.IsRoadDecoration,
        FactionAffinityCsv = ToCsv(m.FactionAffinity),
        BiomeFilterCsv = ToCsv(m.BiomeFilter),
    };

    public ZoneContentItem ToModel() => new()
    {
        Sid = Sid,
        MinCount = MinCount,
        MaxCount = MaxCount,
        Pool = Pool,
        IsGuarded = IsGuarded,
        NearCastle = NearCastle,
        IsRoadDecoration = IsRoadDecoration,
        FactionAffinity = FromCsv(FactionAffinityCsv),
        BiomeFilter = FromCsv(BiomeFilterCsv),
    };

    private static string ToCsv(IEnumerable<string>? items) =>
        items is null ? "" : string.Join(", ", items);

    private static List<string> FromCsv(string csv) =>
        string.IsNullOrWhiteSpace(csv) ? new()
        : csv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

(Read the actual `ZoneContentItem` model to confirm field names — adjust the VM if any differ.)

**Step 4: Run tests, expect PASS.**

**Step 5: Commit + push, watch CI.**

```bash
git commit -m "feat(wpf): add ZoneContentItemViewModel with INPC + CSV round-trip"
```

---

### Task A3: `ZoneContentScopeViewModel` (TDD)

**Files:**
- Create: `src/OldenEra.TemplateEditor/ViewModels/ZoneContentScopeViewModel.cs`
- Create: `tests/OldenEra.TemplateEditor.Tests/ViewModels/ZoneContentScopeViewModelTests.cs`

**Step 1: Write failing tests**

- `FromModel(IEnumerable<ZoneContentItem>)` populates `Items` with VMs.
- `ToModels()` returns equivalent items.
- Adding an item to `Items` raises `CollectionChanged`.
- `ScopeLabel` is set from constructor.

**Step 2-5: Standard TDD cycle.**

**Implementation skeleton:**

```csharp
public sealed class ZoneContentScopeViewModel
{
    public string ScopeLabel { get; }
    public string? PerZoneLetter { get; init; }
    public ObservableCollection<ZoneContentItemViewModel> Items { get; } = new();
    public ZoneContentScopeViewModel(string label) { ScopeLabel = label; }
    public static ZoneContentScopeViewModel From(string label, IEnumerable<ZoneContentItem> items) { ... }
    public IReadOnlyList<ZoneContentItem> ToModels() => Items.Select(vm => vm.ToModel()).ToList();
}
```

**Commit:** `feat(wpf): add ZoneContentScopeViewModel`

---

### Task A4: `ZoneContentPanelViewModel` (TDD)

**Files:**
- Create: `src/OldenEra.TemplateEditor/ViewModels/ZoneContentPanelViewModel.cs`
- Create: `tests/OldenEra.TemplateEditor.Tests/ViewModels/ZoneContentPanelViewModelTests.cs`

**Step 1: Write failing tests**

Cover:
- Constructor accepts `Settings`, exposes seven scope VMs (`PlayerScope`, `NeutralGlobalScope`, `PoorScope`, `NormalScope`, `RichScope`, `PerZoneScope`, `RoadDecorationsScope`).
- Toggling `IsDefaultsCompareActive` to `true` swaps internal settings to `ZoneContentCloning.CloneWithDefaultsBlanked(original)` and sets `IsReadOnly = true`. Toggling back restores.
- `Changed` event raised after `CommitToSettings()`.
- `WarningProjection` matches `ZoneContentWarningProjection.Project(Settings)`.

**Step 2-5: Standard TDD cycle.**

**Commit:** `feat(wpf): add ZoneContentPanelViewModel with defaults-compare toggle`

---

### Task A5: WPF panel shell

**Files:**
- Create: `src/OldenEra.TemplateEditor/Views/ZoneContentPanel.xaml`
- Create: `src/OldenEra.TemplateEditor/Views/ZoneContentPanel.xaml.cs`

**Step 1: Create empty UserControl**

```xml
<UserControl x:Class="OldenEra.TemplateEditor.Views.ZoneContentPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal">
            <ToggleButton Content="Defaults compare" IsChecked="{Binding IsDefaultsCompareActive}" />
            <TextBlock Text="{Binding TotalWarningCount, StringFormat=Warnings: {0}}" Margin="12,0,0,0" />
        </StackPanel>
        <TabControl>
            <TabItem Header="Player" />
            <TabItem Header="Neutral Global" />
            <TabItem Header="Poor" />
            <TabItem Header="Normal" />
            <TabItem Header="Rich" />
            <TabItem Header="Per-zone" />
            <TabItem Header="Road decorations" />
        </TabControl>
    </DockPanel>
</UserControl>
```

Code-behind: empty `InitializeComponent()`.

**Step 2: Verify locally** — file edits only, can't build on Mac.

**Step 3: Commit + push, watch CI.** Goal: CI green = BAML compiles.

```bash
git commit -m "feat(wpf): scaffold ZoneContentPanel UserControl"
```

---

### Task A6: Item row DataTemplate

**Files:**
- Modify: `src/OldenEra.TemplateEditor/Views/ZoneContentPanel.xaml` (or split into a new `ZoneContentItemRow.xaml` ResourceDictionary)
- Reference: read existing item-row template from `src/OldenEra.Web/Components/ZoneContent/ZoneContentItemRow.razor` to match field order and labels.

Bindings: `Sid` (editable ComboBox sourced from `ZoneContentSidCatalog.All()`, `DisplayMemberPath="FriendlyName"`, `SelectedValuePath="Sid"`, `IsTextSearchEnabled=true`), `MinCount`/`MaxCount` (TextBox with `UpdateSourceTrigger=PropertyChanged`), `Pool` (ComboBox over enum), three checkboxes (Guarded, NearCastle, RoadDecoration), `FactionAffinityCsv`/`BiomeFilterCsv` TextBoxes, delete Button.

**Step 1: Add DataTemplate scoped to `ZoneContentItemViewModel`.**
**Step 2: Wire `TabItem` content to render `ItemsControl` over the relevant scope's `Items` for one tab (Player) as a smoke test.**
**Step 3: Commit + push, watch CI.**

```bash
git commit -m "feat(wpf): item row DataTemplate"
```

---

### Task A7: All seven scope tabs render

**Files:**
- Modify: `src/OldenEra.TemplateEditor/Views/ZoneContentPanel.xaml`

Replicate the Player-tab rendering pattern across the other six tabs. Per-zone tab gets a horizontal split with a letter `ListBox` on the left.

**Step 1: Wire all seven tabs.**
**Step 2: Commit + push, watch CI.**

```bash
git commit -m "feat(wpf): wire all seven zone-content scope tabs"
```

---

### Task A8: Warning badges (per-row + per-field + tab-header)

**Files:**
- Modify: `src/OldenEra.TemplateEditor/Views/ZoneContentPanel.xaml`
- Create: `src/OldenEra.TemplateEditor/Converters/ZoneContentSeverityToBrushConverter.cs`
- Modify: `ZoneContentPanelViewModel` to expose `WarningProjection` indexed by scope/item/field (already designed; this task just wires it through bindings).

**Step 1: Per-row aggregated badge.** Small `Border` with bound `BorderBrush` (severity → color), Visibility = `Collapsed` when warning count is 0.

**Step 2: Per-field badges.** Same pattern, scoped per-field.

**Step 3: Tab-header badges.** `TabItem.Header` becomes a `StackPanel` with label + badge.

**Step 4: Commit + push, watch CI.**

```bash
git commit -m "feat(wpf): warning badges on rows, fields, and tabs"
```

---

### Task A9: Preset picker grouped ComboBox

**Files:**
- Modify: `src/OldenEra.TemplateEditor/Views/ZoneContentPanel.xaml`

**Step 1: Add `CollectionViewSource` resource keyed off `ZoneContentPresets.All()` with grouping by `Category`.**

```xml
<CollectionViewSource x:Key="GroupedPresets" Source="{x:Static ...}">
    <CollectionViewSource.GroupDescriptions>
        <PropertyGroupDescription PropertyName="Category" />
    </CollectionViewSource.GroupDescriptions>
</CollectionViewSource>
```

(Static binding to a `static IReadOnlyList<ZoneContentPreset>` requires either a markup-extension wrapper or exposing it via the panel's DataContext. Simpler: expose `IReadOnlyList<ZoneContentPreset> Presets => ZoneContentPresets.All()` on `ZoneContentPanelViewModel`.)

**Step 2: Add per-tab preset `ComboBox` styled "+ Add from preset…" with `GroupStyle` showing bold category headers.**

**Step 3: Wire `SelectionChanged` to a command that calls `Items.Add(ZoneContentItemViewModel.FromModel(preset.Item))` and resets the selection.**

**Step 4: Commit + push, watch CI.**

```bash
git commit -m "feat(wpf): preset picker grouped by category"
```

---

### Task A10: MainWindow integration

**Files:**
- Modify: `src/OldenEra.TemplateEditor/MainWindow.xaml`
- Modify: `src/OldenEra.TemplateEditor/MainWindow.xaml.cs`

**Step 1: Place `ZoneContentPanel` in the main window's existing tab/section.** Find the equivalent location to where Round 4 placed it on the Web side.

**Step 2: Wire `ZoneContentPanelViewModel.Changed` to the existing preview-rebuild path.**

**Step 3: On save, call `panelViewModel.CommitToSettings()` before serializing.**

**Step 4: Commit + push, watch CI.**

```bash
git commit -m "feat(wpf): integrate ZoneContentPanel into MainWindow"
```

---

### Task A11: Fresh-eyes review pass

Use `superpowers:requesting-code-review` skill to dispatch a code review against the design doc and Round 4 Web implementation. Address findings, push follow-up commits.

---

### Task A12: Open Thread A PR

```bash
gh pr create --repo rannes/Olden-Era---Template-Generator --base main --head feature/zone-content-round-5 --title "feat: zone-content round 5 — thread A (WPF port)" --body "$(cat <<'EOF'
## Summary
- WPF port of the Round 4 zone-content editor.
- INPC view-model wrappers in OldenEra.TemplateEditor; pure-C# tests in new OldenEra.TemplateEditor.Tests project (builds on Mac).
- Sid combobox bound to ZoneContentSidCatalog; preset picker grouped by category.
- Defaults-compare toggle, validator badges (per-row, per-field, per-tab).

## Test plan
- [x] OldenEra.Generator.Tests pass on Mac.
- [x] OldenEra.TemplateEditor.Tests pass on Mac.
- [x] CI green (BAML compile + cross-platform tests).
- [ ] Manual smoke on Windows (after merge).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Definition of done

- Both PRs merged into `main` of the fork.
- All four host-shared zone-content services (`SidCatalog`, `Presets`, `Cloning`, `Scope`, `WarningProjection`) live in `OldenEra.Generator`.
- Web Sid input has datalist autocomplete; Web preset picker is `<optgroup>`-grouped.
- WPF `ZoneContentPanel` is functional with all seven scopes, defaults-compare toggle, validator badges, preset picker, and Sid autocomplete.
- VM tests in `OldenEra.TemplateEditor.Tests` run on Mac.
- No regressions in emitter/library tests.
