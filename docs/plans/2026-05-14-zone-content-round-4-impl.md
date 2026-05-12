# Zone-content Round 4 — implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or superpowers:subagent-driven-development) to implement this plan task-by-task.

**Goal:** Build a Web (Blazor WASM) UI for the customizable zone-content surface (player + neutral global/by-tier/by-zone-letter + road decorations) gated behind the existing experimental master toggle, with row-level preset insertion, inline validator-warning badges, and a defaults-compare toggle.

**Architecture:** New `ZoneContentEditor.razor` component subtree under `src/OldenEra.Web/Components/ZoneContent/`, embedded in `ExperimentalZonePanel.razor` as a single `ExperimentalCard`. Tab-strip master+detail layout (Player / Neutral Global / Neutral Poor / Normal / Rich / Per-zone / Road decorations). Reuses `ZoneContentEmitWarnings.Inspect` from the generator project — no new validation logic. Cloning helper centralizes the `SettingsMapper.cs:161/271` aliasing escape.

**Tech stack:** .NET 10, Blazor WASM, xUnit. No new package references. Shared `OldenEra.Generator` project already referenced from `OldenEra.Web.csproj` and both test projects.

**Design:** [`docs/plans/2026-05-14-zone-content-round-4-design.md`](./2026-05-14-zone-content-round-4-design.md)

---

## Pre-flight

**Worktree.** Work in `.worktrees/zone-content-round-4` on branch `feature/zone-content-round-4` cut from `main`. Create via `superpowers:using-git-worktrees`.

**Mac dev commands.**
- Generator build: `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj`
- Generator tests: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`
- TemplateEditor.Tests (xUnit, builds on Mac despite the WPF host not building): `dotnet test tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj`
- Web build: `dotnet build src/OldenEra.Web/OldenEra.Web.csproj`
- Web dev server: `dotnet watch run --project src/OldenEra.Web/OldenEra.Web.csproj` (NEVER `dotnet run` — stale-fingerprint trap)

**PR target.** `gh pr create --repo rannes/Olden-Era---Template-Generator` — repo is a fork.

**Where new web-side tests go.** A new test project: `tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj` (xUnit, references `OldenEra.Web.csproj`). Justification: the web cloning helper and warning projection live in the web project's namespace and aren't reachable from the existing test projects without taking on a project ref to the Blazor WASM SDK output. Task 0 sets this up.

---

## Task 0: Web tests project scaffold

**Why first:** Tasks 1 and 2 are TDD; they need the test project to exist.

**Files:**
- Create: `tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj`
- Create: `tests/OldenEra.Web.Tests/Smoke.cs`

**Step 1: Write the csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>OldenEra.Web.Tests</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\OldenEra.Web\OldenEra.Web.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

**Step 2: Write a smoke test**

`tests/OldenEra.Web.Tests/Smoke.cs`:

```csharp
namespace OldenEra.Web.Tests;

public class Smoke
{
    [Fact]
    public void Ok() => Assert.True(true);
}
```

**Step 3: Run**

```
dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj
```

Expected: 1 passed. If the test project fails to reference the WASM csproj cleanly (the WASM SDK normally only restores for browser targets), fall back to placing the cloning helper + warning projection in `OldenEra.Generator` instead and using `OldenEra.Generator.Tests`. Document the chosen path in the task's commit message.

**Step 4: Commit**

```bash
git add tests/OldenEra.Web.Tests
git commit -m "test(web): add OldenEra.Web.Tests xUnit project for round 4"
```

---

## Task 1: ZoneContentCloning helper + tests

**Why:** Round 3's `SettingsMapper.cs:161` and `:271` alias the four zone-content trees. The UI must clone before mutating list shape. Centralize this so callers don't reinvent it.

**Files:**
- Create: `src/OldenEra.Web/Services/ZoneContentCloning.cs`
- Create: `tests/OldenEra.Web.Tests/Services/ZoneContentCloningTests.cs`

**Step 1: Write the failing tests**

`tests/OldenEra.Web.Tests/Services/ZoneContentCloningTests.cs`:

```csharp
using OldenEra.Generator.Models;
using OldenEra.Web.Services;

namespace OldenEra.Web.Tests.Services;

public class ZoneContentCloningTests
{
    [Fact]
    public void CloneList_ReturnsIndependentItems()
    {
        var original = new ZoneContentList();
        original.Items.Add(new ZoneContentItem { Sid = "x", Handle = "h", MinCount = 2, MaxCount = 3 });

        var clone = ZoneContentCloning.CloneList(original);
        clone.Items[0].Sid = "MUTATED";
        clone.Items[0].FactionAffinity.Add("haven");

        Assert.Equal("x", original.Items[0].Sid);
        Assert.Empty(original.Items[0].FactionAffinity);
        Assert.NotSame(original.Items, clone.Items);
        Assert.NotSame(original.Items[0], clone.Items[0]);
    }

    [Fact]
    public void CloneNeutral_ProducesIndependentTierAndZoneDictionaries()
    {
        var original = new NeutralZoneContent();
        original.Global.Items.Add(new ZoneContentItem { Sid = "g" });
        original.ByTier[NeutralZoneTier.Normal] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "t" } },
        };
        original.ByZoneLetter["A"] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "z" } },
        };

        var clone = ZoneContentCloning.CloneNeutral(original);
        clone.Global.Items.Clear();
        clone.ByTier[NeutralZoneTier.Normal].Items.Clear();
        clone.ByZoneLetter["A"].Items.Clear();
        clone.ByZoneLetter["B"] = new ZoneContentList();

        Assert.Single(original.Global.Items);
        Assert.Single(original.ByTier[NeutralZoneTier.Normal].Items);
        Assert.Single(original.ByZoneLetter["A"].Items);
        Assert.False(original.ByZoneLetter.ContainsKey("B"));
    }

    [Fact]
    public void CloneRoadDecorations_IsIndependent()
    {
        var original = new List<ZoneRoadDecoration>
        {
            new() { Zone = "A", RoadType = ZoneRoadType.Stone },
        };

        var clone = ZoneContentCloning.CloneRoadDecorations(original);
        clone[0].Zone = "Z";
        clone.Add(new ZoneRoadDecoration());

        Assert.Equal("A", original[0].Zone);
        Assert.Single(original);
    }

    [Fact]
    public void CloneWithDefaultsBlanked_PreservesNonZoneContent_AndBlankZoneTrees()
    {
        var settings = new GeneratorSettings { Seed = 42 };
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "p" });
        settings.NeutralZoneContent.Global.Items.Add(new ZoneContentItem { Sid = "n" });
        settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration { Zone = "B" });

        var clone = ZoneContentCloning.CloneWithDefaultsBlanked(settings);

        Assert.Equal(42, clone.Seed);
        Assert.Empty(clone.PlayerZoneContent.Items);
        Assert.Empty(clone.NeutralZoneContent.Global.Items);
        Assert.Empty(clone.NeutralZoneContent.ByTier);
        Assert.Empty(clone.NeutralZoneContent.ByZoneLetter);
        Assert.Empty(clone.ZoneRoadDecorations);
        // and the source is untouched:
        Assert.Single(settings.PlayerZoneContent.Items);
    }
}
```

**Step 2: Run, expect compile failure**

```
dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj
```

Expected: FAIL — `ZoneContentCloning` does not exist.

**Step 3: Implement**

`src/OldenEra.Web/Services/ZoneContentCloning.cs`:

```csharp
using OldenEra.Generator.Models;

namespace OldenEra.Web.Services;

public static class ZoneContentCloning
{
    public static ZoneContentList CloneList(ZoneContentList source) => new()
    {
        Items = source.Items.Select(CloneItem).ToList(),
    };

    public static NeutralZoneContent CloneNeutral(NeutralZoneContent source) => new()
    {
        Global = CloneList(source.Global),
        ByTier = source.ByTier.ToDictionary(kv => kv.Key, kv => CloneList(kv.Value)),
        ByZoneLetter = source.ByZoneLetter.ToDictionary(kv => kv.Key, kv => CloneList(kv.Value)),
    };

    public static List<ZoneRoadDecoration> CloneRoadDecorations(List<ZoneRoadDecoration> source) =>
        source.Select(CloneRoadDecoration).ToList();

    public static ZoneContentItem CloneItem(ZoneContentItem source) => new()
    {
        Sid = source.Sid,
        Handle = source.Handle,
        IsGroup = source.IsGroup,
        MinCount = source.MinCount,
        MaxCount = source.MaxCount,
        Pool = source.Pool,
        IsGuarded = source.IsGuarded,
        NearCastle = source.NearCastle,
        RoadDistance = source.RoadDistance,
        FactionAffinity = source.FactionAffinity.ToList(),
        BiomeFilter = source.BiomeFilter.ToList(),
    };

    public static ZoneRoadDecoration CloneRoadDecoration(ZoneRoadDecoration source) => new()
    {
        Zone = source.Zone,
        RoadType = source.RoadType,
        From = CloneEndpoint(source.From),
        To = CloneEndpoint(source.To),
    };

    private static ZoneRoadEndpoint CloneEndpoint(ZoneRoadEndpoint source) => new()
    {
        // Mirror whatever fields ZoneRoadEndpoint has — verify by reading the type before coding.
    };

    /// <summary>
    /// Returns a settings clone with all four zone-content trees blanked so the
    /// preview pipeline can render "what the generator would produce by default"
    /// without disturbing the user's edits.
    /// </summary>
    public static GeneratorSettings CloneWithDefaultsBlanked(GeneratorSettings source)
    {
        // Shallow-clone the rest of the settings; only the zone-content trees
        // need blanking. The simplest correct approach is to reuse the existing
        // round-trip via SettingsMapper if appropriate, otherwise mutate-by-reference
        // is fine because the page only hands a transient instance to the preview.
        // Implementation note: verify what's safe by reading SettingsMapper before
        // committing this approach.
        var clone = source; // PLACEHOLDER — replace with proper clone path.
        clone.PlayerZoneContent = new ZoneContentList();
        clone.NeutralZoneContent = new NeutralZoneContent();
        clone.ZoneRoadDecorations = new List<ZoneRoadDecoration>();
        return clone;
    }
}
```

**IMPORTANT:** Before writing the impl, the executing agent must:
1. Read `src/OldenEra.Generator/Models/Generator/ZoneRoadEndpoint.cs` and `ZoneRoadType.cs` to fill in `CloneEndpoint` correctly.
2. Decide how `CloneWithDefaultsBlanked` clones the *rest* of `GeneratorSettings`. The placeholder above mutates the source in place which violates the contract. Two acceptable approaches:
   - (a) Use `SettingsShareCodec` round-trip (encode then decode produces a deep clone).
   - (b) Add a `CloneSettings(GeneratorSettings)` that copies the four zone-content trees deeply and shallow-aliases everything else (acceptable because the preview pipeline only reads, never writes).
   Pick (b) — cheaper, no JSON round-trip per defaults-toggle render. Implement it as a `CloneShallowExceptZoneContent` helper that creates a new `GeneratorSettings`, copies all properties via reflection or by hand, then overwrites the four zone-content properties with new empty instances.
   - **Re-evaluate:** if reflection feels heavy, the simplest path is `SettingsShareCodec` round-trip — used once on toggle, not per keystroke. Use that. Document the choice in the commit message.

**Step 4: Run tests**

```
dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj
```

Expected: 4 passed.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Services/ZoneContentCloning.cs \
        tests/OldenEra.Web.Tests/Services/ZoneContentCloningTests.cs
git commit -m "feat(web): ZoneContentCloning helper centralizes mapper-aliasing escape

UI must clone before mutating zone-content list shape because
SettingsMapper aliases the trees (see SettingsMapper.cs:161,271).
Helper covers item lists, the neutral surface, road decorations,
and a defaults-blanked settings clone for the inspect-defaults UX."
```

---

## Task 2: ZoneContentWarningProjection + tests

**Why:** The detail pane and list rows need slices of validator warnings keyed by `(scope, handle)`. Reuse `ZoneContentEmitWarnings.Inspect`; this task only owns the iteration and keying.

**Files:**
- Create: `src/OldenEra.Web/Services/ZoneContentScope.cs`
- Create: `src/OldenEra.Web/Services/ZoneContentWarningProjection.cs`
- Create: `tests/OldenEra.Web.Tests/Services/ZoneContentWarningProjectionTests.cs`

**Step 1: Define the scope key and warnings shape**

`src/OldenEra.Web/Services/ZoneContentScope.cs`:

```csharp
using OldenEra.Generator.Models;

namespace OldenEra.Web.Services;

public enum ZoneContentScopeKind
{
    Player,
    NeutralGlobal,
    NeutralPoor,
    NeutralNormal,
    NeutralRich,
    NeutralPerZone,
    RoadDecorations,
}

public readonly record struct ZoneContentScopeKey(ZoneContentScopeKind Kind, string? ZoneLetter = null)
{
    public static ZoneContentScopeKey FromTier(NeutralZoneTier tier) => tier switch
    {
        NeutralZoneTier.Poor => new(ZoneContentScopeKind.NeutralPoor),
        NeutralZoneTier.Normal => new(ZoneContentScopeKind.NeutralNormal),
        NeutralZoneTier.Rich => new(ZoneContentScopeKind.NeutralRich),
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };
}
```

**Step 2: Failing tests**

`tests/OldenEra.Web.Tests/Services/ZoneContentWarningProjectionTests.cs`:

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.Web.Services;

namespace OldenEra.Web.Tests.Services;

public class ZoneContentWarningProjectionTests
{
    [Fact]
    public void PlayerItem_WithBiomeFilter_ProducesBiomeIgnoredWarning()
    {
        var settings = new GeneratorSettings();
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "x", Handle = "h1", BiomeFilter = { "snow" },
        });

        var result = ZoneContentWarningProjection.Project(settings);

        var key = (new ZoneContentScopeKey(ZoneContentScopeKind.Player), "h1");
        Assert.Contains(result, w => w.Scope == key.Item1 && w.Handle == "h1"
            && w.Warning.Code == EmitWarning.Codes.BiomeFilterIgnored);
    }

    [Fact]
    public void NeutralByTier_WithNonMandatoryPool_ProducesPoolDroppedWarning_KeyedByTier()
    {
        var settings = new GeneratorSettings();
        settings.NeutralZoneContent.ByTier[NeutralZoneTier.Rich] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "y", Handle = "rich1", Pool = ZoneContentPool.Resources } },
        };

        var result = ZoneContentWarningProjection.Project(settings);

        Assert.Contains(result, w => w.Scope.Kind == ZoneContentScopeKind.NeutralRich
            && w.Handle == "rich1"
            && w.Warning.Code == EmitWarning.Codes.PoolNonMandatoryDropped);
    }

    [Fact]
    public void NeutralByZoneLetter_KeyedByLetter()
    {
        var settings = new GeneratorSettings();
        settings.NeutralZoneContent.ByZoneLetter["B"] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "z", Handle = "zb", FactionAffinity = { "haven" } } },
        };

        var result = ZoneContentWarningProjection.Project(settings);

        Assert.Contains(result, w => w.Scope.Kind == ZoneContentScopeKind.NeutralPerZone
            && w.Scope.ZoneLetter == "B"
            && w.Handle == "zb"
            && w.Warning.Code == EmitWarning.Codes.FactionAffinityIgnored);
    }

    [Fact]
    public void EmptySurface_ProducesNoWarnings()
    {
        var result = ZoneContentWarningProjection.Project(new GeneratorSettings());
        Assert.Empty(result);
    }
}
```

**Step 3: Implement projection**

`src/OldenEra.Web/Services/ZoneContentWarningProjection.cs`:

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;

namespace OldenEra.Web.Services;

public sealed record ZoneContentWarning(
    ZoneContentScopeKey Scope,
    string? Handle,
    int ItemIndex,
    EmitWarning Warning);

public static class ZoneContentWarningProjection
{
    public static IReadOnlyList<ZoneContentWarning> Project(GeneratorSettings settings)
    {
        var result = new List<ZoneContentWarning>();

        Inspect(settings.PlayerZoneContent, new ZoneContentScopeKey(ZoneContentScopeKind.Player), zoneName: "Player", result);

        Inspect(settings.NeutralZoneContent.Global, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralGlobal), zoneName: "Neutral", result);

        foreach (var (tier, list) in settings.NeutralZoneContent.ByTier)
            Inspect(list, ZoneContentScopeKey.FromTier(tier), zoneName: $"Neutral.{tier}", result);

        foreach (var (letter, list) in settings.NeutralZoneContent.ByZoneLetter)
            Inspect(list, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPerZone, letter), zoneName: letter, result);

        return result;
    }

    private static void Inspect(
        ZoneContentList list,
        ZoneContentScopeKey scope,
        string? zoneName,
        List<ZoneContentWarning> result)
    {
        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            foreach (var w in ZoneContentEmitWarnings.Inspect(item, zoneName))
                result.Add(new ZoneContentWarning(scope, item.Handle, i, w));
        }
    }
}
```

**Step 4: Run**

```
dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj
```

Expected: 4 new tests pass; total 8.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Services/ZoneContentScope.cs \
        src/OldenEra.Web/Services/ZoneContentWarningProjection.cs \
        tests/OldenEra.Web.Tests/Services/ZoneContentWarningProjectionTests.cs
git commit -m "feat(web): ZoneContentWarningProjection slices EmitWarnings by scope+handle

Reuses ZoneContentEmitWarnings.Inspect from the generator —
the projection only owns iteration over the four trees and
keying by (scope, handle, item-index)."
```

---

## Task 3: ZoneContentEditor shell + ScopeTabs (no edits yet)

**Why:** Get the chrome and selection state in place before wiring item editing. This task only renders the tab strip and a placeholder pane per tab; no list, no detail.

**Files:**
- Create: `src/OldenEra.Web/Components/ZoneContent/ZoneContentEditor.razor`
- Create: `src/OldenEra.Web/Components/ZoneContent/ZoneContentScopeTabs.razor`
- Create: `src/OldenEra.Web/Components/ZoneContent/PerZoneOverridesPicker.razor`
- Modify: `src/OldenEra.Web/Components/ExperimentalZonePanel.razor` — add an `ExperimentalCard` hosting `<ZoneContentEditor>`.

**Step 1: Tabs component**

`src/OldenEra.Web/Components/ZoneContent/ZoneContentScopeTabs.razor`:

```razor
@using OldenEra.Web.Services

<div class="oe-tabstrip" role="tablist">
    @foreach (var tab in Tabs)
    {
        var active = tab.Kind == Selected.Kind;
        <button type="button"
                role="tab"
                class="oe-tab @(active ? "active" : "")"
                aria-selected="@active"
                @onclick="@(() => Choose(tab.Kind))">
            @tab.Label
        </button>
    }
</div>

@code {
    [Parameter, EditorRequired] public ZoneContentScopeKey Selected { get; set; }
    [Parameter] public EventCallback<ZoneContentScopeKey> SelectedChanged { get; set; }

    private static readonly (ZoneContentScopeKind Kind, string Label)[] Tabs =
    {
        (ZoneContentScopeKind.Player,          "Player"),
        (ZoneContentScopeKind.NeutralGlobal,   "Neutral · Global"),
        (ZoneContentScopeKind.NeutralPoor,     "Neutral · Poor"),
        (ZoneContentScopeKind.NeutralNormal,   "Neutral · Normal"),
        (ZoneContentScopeKind.NeutralRich,     "Neutral · Rich"),
        (ZoneContentScopeKind.NeutralPerZone,  "Per-zone"),
        (ZoneContentScopeKind.RoadDecorations, "Road decorations"),
    };

    private Task Choose(ZoneContentScopeKind kind) =>
        SelectedChanged.InvokeAsync(new ZoneContentScopeKey(kind));
}
```

**Step 2: Per-zone picker stub**

`src/OldenEra.Web/Components/ZoneContent/PerZoneOverridesPicker.razor`:

```razor
@using OldenEra.Generator.Models

<div class="oe-stacked">
    <label class="oe-label">Zone letter</label>
    <div class="oe-row" style="grid-template-columns: 1fr 100px;">
        <select @bind="SelectedLetter" @bind:after="NotifyChanged" disabled="@(!HasOverrides)">
            @if (!HasOverrides)
            {
                <option value="">(none yet)</option>
            }
            @foreach (var letter in Neutral.ByZoneLetter.Keys.OrderBy(x => x))
            {
                <option value="@letter">@letter</option>
            }
        </select>
        <button type="button" class="oe-btn ghost" @onclick="AddLetter">+ Add letter</button>
    </div>
    <input type="text" placeholder="New letter (e.g. A)" @bind="PendingLetter" />
</div>

@code {
    [Parameter, EditorRequired] public NeutralZoneContent Neutral { get; set; } = default!;
    [Parameter] public string SelectedLetter { get; set; } = "";
    [Parameter] public EventCallback<string> SelectedLetterChanged { get; set; }
    [Parameter] public EventCallback OnNeutralChanged { get; set; }

    private string PendingLetter { get; set; } = "";
    private bool HasOverrides => Neutral.ByZoneLetter.Count > 0;

    private async Task NotifyChanged()
    {
        await SelectedLetterChanged.InvokeAsync(SelectedLetter);
    }

    private async Task AddLetter()
    {
        var letter = PendingLetter.Trim();
        if (string.IsNullOrEmpty(letter) || Neutral.ByZoneLetter.ContainsKey(letter)) return;
        Neutral.ByZoneLetter[letter] = new OldenEra.Generator.Models.ZoneContentList();
        PendingLetter = "";
        SelectedLetter = letter;
        await SelectedLetterChanged.InvokeAsync(SelectedLetter);
        await OnNeutralChanged.InvokeAsync();
    }
}
```

**Step 3: Editor shell**

`src/OldenEra.Web/Components/ZoneContent/ZoneContentEditor.razor`:

```razor
@using OldenEra.Generator.Models
@using OldenEra.Web.Services

<div class="oe-zone-content-editor">
    <ZoneContentScopeTabs Selected="@_selected"
                          SelectedChanged="@OnScopeChanged" />

    @if (_selected.Kind == ZoneContentScopeKind.NeutralPerZone)
    {
        <PerZoneOverridesPicker Neutral="@Settings.NeutralZoneContent"
                                SelectedLetter="@_selectedLetter"
                                SelectedLetterChanged="@(letter => { _selectedLetter = letter; })"
                                OnNeutralChanged="@NotifyChanged" />
    }

    <div class="oe-zone-content-body">
        <div class="oe-hint">Selection: @ScopeDescription()</div>
        <p class="oe-hint">Item list and detail land in Task 4.</p>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public GeneratorSettings Settings { get; set; } = default!;
    [Parameter] public EventCallback OnChanged { get; set; }

    private ZoneContentScopeKey _selected = new(ZoneContentScopeKind.Player);
    private string _selectedLetter = "";

    private Task OnScopeChanged(ZoneContentScopeKey next)
    {
        _selected = next;
        return Task.CompletedTask;
    }

    private Task NotifyChanged() => OnChanged.InvokeAsync();

    private string ScopeDescription() => _selected.Kind switch
    {
        ZoneContentScopeKind.NeutralPerZone =>
            string.IsNullOrEmpty(_selectedLetter)
                ? "Per-zone (no letter selected)"
                : $"Per-zone · {_selectedLetter}",
        _ => _selected.Kind.ToString(),
    };
}
```

**Step 4: Wire into experimental panel**

In `src/OldenEra.Web/Components/ExperimentalZonePanel.razor`, append a new `ExperimentalCard` after the existing `content-control` card:

```razor
<ExperimentalCard Key="zone-content" Title="Zone content">
    <div class="oe-hint">
        Author the zone-content surface (player zones, neutral tiers,
        per-zone overrides, road decorations).
    </div>
    <ZoneContentEditor Settings="Settings" OnChanged="OnChanged" />
</ExperimentalCard>
```

**Step 5: Verify build + manual check**

```
dotnet build src/OldenEra.Web/OldenEra.Web.csproj
```

Expected: success. Then:

```
dotnet watch run --project src/OldenEra.Web/OldenEra.Web.csproj
```

Open the browser, enable the experimental master toggle, find the **Zone content** card. Click each tab; selection updates. Per-zone tab shows the letter picker. Open DevTools console — must be free of Blazor errors.

**Step 6: Commit**

```bash
git add src/OldenEra.Web/Components/ZoneContent/ \
        src/OldenEra.Web/Components/ExperimentalZonePanel.razor
git commit -m "feat(web): zone-content editor shell + scope tabs

Tab strip across Player / Neutral Global / Poor / Normal / Rich /
Per-zone / Road decorations. Per-zone tab adds a nested zone-letter
picker. Editor body is a placeholder; item list and detail follow
in task 4."
```

---

## Task 4: ZoneContentItemList + ZoneContentItemDetail

**Why:** Make the editor functional for the seven item-bearing scopes (everything except Road decorations).

**Files:**
- Create: `src/OldenEra.Web/Components/ZoneContent/ZoneContentItemList.razor`
- Create: `src/OldenEra.Web/Components/ZoneContent/ZoneContentItemDetail.razor`
- Modify: `src/OldenEra.Web/Components/ZoneContent/ZoneContentEditor.razor` — replace placeholder with list + detail; resolve `List<ZoneContentItem>` per scope.

**Step 1: List component**

`src/OldenEra.Web/Components/ZoneContent/ZoneContentItemList.razor`:

```razor
@using OldenEra.Generator.Models
@using OldenEra.Web.Services

<div class="oe-zc-list">
    <div class="oe-zc-list-toolbar">
        <button type="button" class="oe-btn ghost" disabled="@ReadOnly" @onclick="AddBlank">+ Add</button>
        @* Preset insertion lands in Task 6. *@
    </div>
    @if (Items.Count == 0)
    {
        <div class="oe-hint">No items yet.</div>
    }
    else
    {
        <ul class="oe-zc-list-rows">
            @foreach (var (item, idx) in Items.Select((it, i) => (it, i)))
            {
                var key = item.Handle ?? $"#{idx}";
                var selected = SelectedKey == key;
                <li class="oe-zc-row @(selected ? "selected" : "")"
                    @onclick="@(() => SelectKey(key))">
                    <span class="oe-zc-row-sid">@item.Sid</span>
                    <span class="oe-zc-row-handle">@(item.Handle ?? "—")</span>
                    <span class="oe-zc-row-count">@item.MinCount-@item.MaxCount</span>
                    <button type="button" class="oe-link-btn"
                            disabled="@ReadOnly"
                            @onclick:stopPropagation
                            @onclick="@(() => Remove(idx))" aria-label="Remove">✕</button>
                </li>
            }
        </ul>
    }
</div>

@code {
    [Parameter, EditorRequired] public List<ZoneContentItem> Items { get; set; } = default!;
    [Parameter] public string? SelectedKey { get; set; }
    [Parameter] public EventCallback<string?> SelectedKeyChanged { get; set; }
    [Parameter] public EventCallback OnItemsChanged { get; set; }
    [Parameter] public bool ReadOnly { get; set; }

    private async Task AddBlank()
    {
        Items.Add(new ZoneContentItem { Sid = "" });
        await OnItemsChanged.InvokeAsync();
    }

    private async Task Remove(int idx)
    {
        if (idx < 0 || idx >= Items.Count) return;
        Items.RemoveAt(idx);
        await OnItemsChanged.InvokeAsync();
    }

    private Task SelectKey(string key) => SelectedKeyChanged.InvokeAsync(key);
}
```

**Note on cloning:** the `Items` parameter is the actual reference owned by `GeneratorSettings`. Mutating it directly works because `SettingsMapper` aliases on read but the file write path snapshot-clones via JSON. For Round 4 the load-bearing concern is when *replacing* trees (preset inserts that swap `ZoneContentList` instances) — `Add`/`RemoveAt` on an existing list is safe and matches every other panel in this app (see `ExperimentalZonePanel.razor:107-111`).

**Step 2: Detail component**

`src/OldenEra.Web/Components/ZoneContent/ZoneContentItemDetail.razor`:

```razor
@using OldenEra.Generator.Models
@using OldenEra.Generator.Services.ZoneContent
@using OldenEra.Web.Services

@if (Item is null)
{
    <div class="oe-hint">Select an item to edit.</div>
}
else
{
    <div class="oe-zc-detail">
        <div class="oe-stacked">
            <label class="oe-label">Sid</label>
            <select @bind="Item.Sid" @bind:after="NotifyChanged" disabled="@ReadOnly">
                <option value="">Pick…</option>
                @foreach (var sid in ZoneContentSidCatalog.All())
                {
                    <option value="@sid">@sid</option>
                }
            </select>
        </div>

        <div class="oe-stacked">
            <label class="oe-label">Handle</label>
            <input type="text" disabled="@ReadOnly"
                   @bind="HandleText" @bind:after="NotifyChanged" />
        </div>

        <div class="oe-row" style="grid-template-columns: 1fr 1fr;">
            <div class="oe-stacked">
                <label class="oe-label">MinCount</label>
                <input type="number" min="0" max="20" disabled="@ReadOnly"
                       @bind="Item.MinCount" @bind:after="NotifyChanged" />
            </div>
            <div class="oe-stacked">
                <label class="oe-label">MaxCount</label>
                <input type="number" min="0" max="20" disabled="@ReadOnly"
                       @bind="Item.MaxCount" @bind:after="NotifyChanged" />
            </div>
        </div>

        <div class="oe-stacked">
            <label class="oe-label">Pool</label>
            <select @bind="Item.Pool" @bind:after="NotifyChanged" disabled="@ReadOnly">
                @foreach (var p in Enum.GetValues<ZoneContentPool>())
                {
                    <option value="@p">@p</option>
                }
            </select>
        </div>

        <div class="oe-row" style="grid-template-columns: 1fr 1fr;">
            <label class="oe-checkbox">
                <input type="checkbox" disabled="@ReadOnly"
                       @bind="Item.IsGuarded" @bind:after="NotifyChanged" />
                Guarded
            </label>
            <label class="oe-checkbox">
                <input type="checkbox" disabled="@ReadOnly"
                       @bind="Item.NearCastle" @bind:after="NotifyChanged" />
                Near castle
            </label>
        </div>

        <div class="oe-stacked">
            <label class="oe-label">RoadDistance</label>
            <select value="@(Item.RoadDistance?.ToString() ?? "")" disabled="@ReadOnly"
                    @onchange="OnRoadDistanceChanged">
                <option value="">(unset)</option>
                @foreach (var d in Enum.GetValues<RoadDistance>())
                {
                    <option value="@d">@d</option>
                }
            </select>
        </div>

        @* FactionAffinity + BiomeFilter as comma-edit text inputs in v1. *@
        <div class="oe-stacked">
            <label class="oe-label">FactionAffinity (comma-separated)</label>
            <input type="text" disabled="@ReadOnly"
                   value="@string.Join(", ", Item.FactionAffinity)"
                   @onchange="OnFactionAffinityChanged" />
        </div>
        <div class="oe-stacked">
            <label class="oe-label">BiomeFilter (comma-separated)</label>
            <input type="text" disabled="@ReadOnly"
                   value="@string.Join(", ", Item.BiomeFilter)"
                   @onchange="OnBiomeFilterChanged" />
        </div>
    </div>
}

@code {
    [Parameter] public ZoneContentItem? Item { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }
    [Parameter] public IReadOnlyList<EmitWarning> Warnings { get; set; } = Array.Empty<EmitWarning>();
    [Parameter] public bool ReadOnly { get; set; }

    private string HandleText
    {
        get => Item?.Handle ?? "";
        set { if (Item is not null) Item.Handle = string.IsNullOrWhiteSpace(value) ? null : value; }
    }

    private Task NotifyChanged() => OnChanged.InvokeAsync();

    private async Task OnRoadDistanceChanged(ChangeEventArgs e)
    {
        if (Item is null) return;
        var v = e.Value?.ToString() ?? "";
        Item.RoadDistance = string.IsNullOrEmpty(v)
            ? null
            : Enum.Parse<RoadDistance>(v);
        await NotifyChanged();
    }

    private async Task OnFactionAffinityChanged(ChangeEventArgs e)
    {
        if (Item is null) return;
        Item.FactionAffinity = SplitCsv(e.Value?.ToString());
        await NotifyChanged();
    }

    private async Task OnBiomeFilterChanged(ChangeEventArgs e)
    {
        if (Item is null) return;
        Item.BiomeFilter = SplitCsv(e.Value?.ToString());
        await NotifyChanged();
    }

    private static List<string> SplitCsv(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? new List<string>()
            : s.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
}
```

**Sid catalog reference:** check `src/OldenEra.Generator/Services/ZoneContent/ZoneContentSidCatalog.cs` for the actual API. If `All()` does not exist, adapt — read the file before coding.

**Step 3: Wire list+detail into the editor**

In `ZoneContentEditor.razor`, replace the placeholder body with:

```razor
@if (_selected.Kind == ZoneContentScopeKind.RoadDecorations)
{
    <div class="oe-hint">Road decorations editor lands in Task 5.</div>
}
else
{
    var items = ResolveItems();
    if (items is null)
    {
        <div class="oe-hint">Pick a zone letter above.</div>
    }
    else
    {
        <div class="oe-zc-master-detail">
            <ZoneContentItemList Items="@items"
                                 SelectedKey="@_selectedKey"
                                 SelectedKeyChanged="@(k => _selectedKey = k)"
                                 OnItemsChanged="@NotifyChanged"
                                 ReadOnly="@false" />
            <ZoneContentItemDetail Item="@ResolveSelectedItem(items)"
                                   OnChanged="@NotifyChanged"
                                   ReadOnly="@false" />
        </div>
    }
}

@code {
    private string? _selectedKey;

    private List<ZoneContentItem>? ResolveItems() => _selected.Kind switch
    {
        ZoneContentScopeKind.Player          => Settings.PlayerZoneContent.Items,
        ZoneContentScopeKind.NeutralGlobal   => Settings.NeutralZoneContent.Global.Items,
        ZoneContentScopeKind.NeutralPoor     => GetOrCreateTier(NeutralZoneTier.Poor).Items,
        ZoneContentScopeKind.NeutralNormal   => GetOrCreateTier(NeutralZoneTier.Normal).Items,
        ZoneContentScopeKind.NeutralRich     => GetOrCreateTier(NeutralZoneTier.Rich).Items,
        ZoneContentScopeKind.NeutralPerZone  => string.IsNullOrEmpty(_selectedLetter)
            ? null
            : GetOrCreateLetter(_selectedLetter).Items,
        _ => null,
    };

    private ZoneContentList GetOrCreateTier(NeutralZoneTier tier)
    {
        if (!Settings.NeutralZoneContent.ByTier.TryGetValue(tier, out var list))
        {
            list = new ZoneContentList();
            Settings.NeutralZoneContent.ByTier[tier] = list;
        }
        return list;
    }

    private ZoneContentList GetOrCreateLetter(string letter)
    {
        if (!Settings.NeutralZoneContent.ByZoneLetter.TryGetValue(letter, out var list))
        {
            list = new ZoneContentList();
            Settings.NeutralZoneContent.ByZoneLetter[letter] = list;
        }
        return list;
    }

    private ZoneContentItem? ResolveSelectedItem(List<ZoneContentItem> items)
    {
        if (_selectedKey is null) return null;
        if (_selectedKey.StartsWith("#") && int.TryParse(_selectedKey[1..], out var idx))
            return idx < items.Count ? items[idx] : null;
        return items.FirstOrDefault(it => it.Handle == _selectedKey);
    }
}
```

Reset `_selectedKey` on scope/letter changes (both setters).

**Step 4: Verify build + manual**

```
dotnet build src/OldenEra.Web/OldenEra.Web.csproj
dotnet watch run --project src/OldenEra.Web/OldenEra.Web.csproj
```

Browser checks:
- Each tab: Add → row appears → row click → detail populates → edits flow back to the row label.
- Per-zone: Add letter → list appears → Add row in that letter's list → switch letter → other letter is preserved.
- Switch tabs and back: items persist (state is on `Settings`).
- Remove: row disappears, detail clears.
- DevTools console: error-free.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Components/ZoneContent/
git commit -m "feat(web): zone-content item list + detail editor

Master+detail for the six item-bearing scopes. Direct mutation
of List<ZoneContentItem> matches the existing experimental panels;
list-instance replacement (preset insertion path) is centralized
later. Detail edits FactionAffinity/BiomeFilter as comma-text in v1."
```

---

## Task 5: ZoneRoadDecorationsEditor

**Why:** The seventh tab. Different shape from items (no Pool/Handle/etc.) so a dedicated component is cleaner than overloading the item editor.

**Files:**
- Create: `src/OldenEra.Web/Components/ZoneContent/ZoneRoadDecorationsEditor.razor`
- Modify: `ZoneContentEditor.razor` — render this component on the `RoadDecorations` tab.

**Step 1: Read the model.** `src/OldenEra.Generator/Models/Generator/ZoneRoadType.cs` and `ZoneRoadEndpoint.cs` define the field shape. Verify before coding.

**Step 2: Component**

`src/OldenEra.Web/Components/ZoneContent/ZoneRoadDecorationsEditor.razor`:

```razor
@using OldenEra.Generator.Models

<div class="oe-zc-roads">
    <div class="oe-zc-list-toolbar">
        <button type="button" class="oe-btn ghost"
                disabled="@ReadOnly" @onclick="AddBlank">+ Add road decoration</button>
    </div>
    @if (Decorations.Count == 0)
    {
        <div class="oe-hint">No road decorations.</div>
    }
    else
    {
        @foreach (var (deco, idx) in Decorations.Select((d, i) => (d, i)))
        {
            <fieldset class="oe-zc-road-row">
                <div class="oe-row" style="grid-template-columns: 80px 1fr 32px;">
                    <input type="text" placeholder="Zone" disabled="@ReadOnly"
                           @bind="deco.Zone" @bind:after="NotifyChanged" />
                    <select @bind="deco.RoadType" @bind:after="NotifyChanged" disabled="@ReadOnly">
                        @foreach (var t in Enum.GetValues<ZoneRoadType>())
                        {
                            <option value="@t">@t</option>
                        }
                    </select>
                    <button type="button" class="oe-link-btn"
                            disabled="@ReadOnly"
                            @onclick="@(() => Remove(idx))" aria-label="Remove">✕</button>
                </div>
                @* From + To endpoint editors — fields depend on ZoneRoadEndpoint model. *@
                <div class="oe-hint">From / To endpoint editors — fill in based on ZoneRoadEndpoint shape.</div>
            </fieldset>
        }
    }
</div>

@code {
    [Parameter, EditorRequired] public List<ZoneRoadDecoration> Decorations { get; set; } = default!;
    [Parameter] public EventCallback OnChanged { get; set; }
    [Parameter] public bool ReadOnly { get; set; }

    private Task NotifyChanged() => OnChanged.InvokeAsync();

    private async Task AddBlank()
    {
        Decorations.Add(new ZoneRoadDecoration());
        await NotifyChanged();
    }

    private async Task Remove(int idx)
    {
        if (idx < 0 || idx >= Decorations.Count) return;
        Decorations.RemoveAt(idx);
        await NotifyChanged();
    }
}
```

**Step 3: Wire in editor**

In `ZoneContentEditor.razor`, replace the `RoadDecorations` placeholder branch:

```razor
@if (_selected.Kind == ZoneContentScopeKind.RoadDecorations)
{
    <ZoneRoadDecorationsEditor Decorations="@Settings.ZoneRoadDecorations"
                               OnChanged="@NotifyChanged" />
}
else { ... existing item-list path ... }
```

**Step 4: Verify build + manual**

Build, run watch, exercise: Add decoration → fill zone+type → switch tabs → return → preserved → remove → gone.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Components/ZoneContent/ZoneRoadDecorationsEditor.razor \
        src/OldenEra.Web/Components/ZoneContent/ZoneContentEditor.razor
git commit -m "feat(web): road decorations editor for zone-content round 4"
```

---

## Task 6: Preset insertion + defaults-compare toggle

**Why:** Both surface controls live at the editor level. Preset insertion is per-list; the toggle is editor-wide.

**Files:**
- Modify: `src/OldenEra.Web/Components/ZoneContent/ZoneContentItemList.razor` — add `+ Add from preset…` dropdown.
- Modify: `src/OldenEra.Web/Components/ZoneContent/ZoneContentEditor.razor` — add `CompareDefaultsOn` toggle, propagate `ReadOnly`, emit defaults-compare signal to the page.
- Modify: `src/OldenEra.Web/Pages/Home.razor` — when `CompareDefaultsOn`, render preview against `ZoneContentCloning.CloneWithDefaultsBlanked(settings)`.

**Step 1: Preset dropdown in list**

In `ZoneContentItemList.razor`, add a preset menu next to the `+ Add` button:

```razor
<div class="oe-zc-list-toolbar">
    <button type="button" class="oe-btn ghost" disabled="@ReadOnly" @onclick="AddBlank">+ Add</button>
    <select disabled="@ReadOnly" @onchange="OnPresetChosen">
        <option value="">+ Add from preset…</option>
        @foreach (var preset in ZoneContentPresets.All())
        {
            <option value="@preset.Name">@preset.Name</option>
        }
    </select>
</div>
```

```csharp
private async Task OnPresetChosen(ChangeEventArgs e)
{
    var name = e.Value?.ToString();
    if (string.IsNullOrEmpty(name)) return;
    var preset = ZoneContentPresets.All().FirstOrDefault(p => p.Name == name);
    if (preset is null) return;
    Items.Add(ZoneContentCloning.CloneItem(preset.Item));
    await OnItemsChanged.InvokeAsync();
    // Reset select back to placeholder after insertion (set state-bound field to "").
}
```

Use `@using OldenEra.Generator.Services.ZoneContent` and `@using OldenEra.Web.Services` at the top.

**Step 2: Defaults-compare toggle in editor**

In `ZoneContentEditor.razor`, add a toggle row above the tabs:

```razor
<div class="oe-zc-toolbar">
    <label class="oe-checkbox">
        <input type="checkbox" @bind="CompareDefaultsOn" @bind:after="OnCompareToggled" />
        Compare against defaults (read-only)
    </label>
    @if (CompareDefaultsOn)
    {
        <span class="oe-banner">Showing defaults — editing paused.</span>
    }
</div>
```

```csharp
private bool CompareDefaultsOn { get; set; }

[Parameter] public EventCallback<bool> CompareDefaultsChanged { get; set; }

private async Task OnCompareToggled()
{
    await CompareDefaultsChanged.InvokeAsync(CompareDefaultsOn);
}
```

Propagate `ReadOnly="@CompareDefaultsOn"` to `ZoneContentItemList`, `ZoneContentItemDetail`, and `ZoneRoadDecorationsEditor`.

**Step 3: Wire toggle into Home.razor**

Read `src/OldenEra.Web/Pages/Home.razor` for the existing preview pipeline. Add a `_compareDefaults` flag bound to the editor's `CompareDefaultsChanged`. When the preview renders, pass `_compareDefaults ? ZoneContentCloning.CloneWithDefaultsBlanked(Settings) : Settings` into the preview's settings argument.

If the preview consumes `Settings` deep through several components, the cleanest path is to compute a `_previewSettings` field on `Home.razor` and pass that wherever the preview reads. Verify before code: the preview component contract is in `Components/PreviewPanel.razor`.

**Step 4: Manual verification**

- Insert via preset: `+ Add from preset…` dropdown shows the four built-ins; picking one appends a new row matching the preset (e.g. "name_mana_well", guarded).
- Toggle defaults-compare: editor inputs disable; preview re-renders without the user's zone-content edits; toggling off restores edits and preview.
- DevTools console: clean.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Components/ZoneContent/ \
        src/OldenEra.Web/Pages/Home.razor
git commit -m "feat(web): zone-content preset insertion + defaults-compare toggle

Preset dropdown appends a clone of the chosen ZoneContentPreset.Item
to the current list. Defaults-compare toggle re-renders the preview
against a defaults-blanked settings clone and locks the editor."
```

---

## Task 7: Warning badges

**Why:** Validator warnings already exist and already drop biome/faction/non-mandatory edits — users need to know without reading the console.

**Files:**
- Create: `src/OldenEra.Web/Components/ZoneContent/ZoneContentWarningBadge.razor`
- Modify: `ZoneContentItemList.razor` — show aggregated badge per row.
- Modify: `ZoneContentItemDetail.razor` — show field-level badges next to the offending control.
- Modify: `ZoneContentEditor.razor` — call `ZoneContentWarningProjection.Project(Settings)` and slice for list/detail.

**Step 1: Badge component**

```razor
@using OldenEra.Generator.Services.ZoneContent

@if (Warnings.Count > 0)
{
    <span class="oe-zc-warning" title="@Tooltip()" role="status">
        ⚠ @Warnings.Count
    </span>
}

@code {
    [Parameter] public IReadOnlyList<EmitWarning> Warnings { get; set; } = Array.Empty<EmitWarning>();
    private string Tooltip() => string.Join("\n", Warnings.Select(w => $"{w.Code}: {w.Message}"));
}
```

**Step 2: Wire in editor**

In `ZoneContentEditor.razor`:

```csharp
private IReadOnlyList<ZoneContentWarning> _warnings = Array.Empty<ZoneContentWarning>();

protected override void OnParametersSet()
{
    _warnings = ZoneContentWarningProjection.Project(Settings);
}

private async Task NotifyChanged()
{
    _warnings = ZoneContentWarningProjection.Project(Settings);
    await OnChanged.InvokeAsync();
}
```

Pass `IReadOnlyDictionary<string, IReadOnlyList<EmitWarning>>` (keyed by handle or `#index`) to the list, and the slice for the selected item to the detail.

**Step 3: List row badge**

Inside the item list row template, render `<ZoneContentWarningBadge Warnings="@RowWarnings(item, idx)" />` next to the row title.

**Step 4: Detail field badges**

In `ZoneContentItemDetail.razor`, render a badge next to each control whose `EmitWarning.Code` matches:
- Pool dropdown: `PoolNonMandatoryDropped`
- MinCount/MaxCount row: `MinCountRangeNarrowedToMax`
- FactionAffinity row: `FactionAffinityIgnored`
- BiomeFilter row: `BiomeFilterIgnored`

```razor
<ZoneContentWarningBadge Warnings="@WarningsFor(EmitWarning.Codes.PoolNonMandatoryDropped)" />
```

```csharp
private IReadOnlyList<EmitWarning> WarningsFor(string code) =>
    Warnings.Where(w => w.Code == code).ToList();
```

**Step 5: Final manual verification**

- Add a player item with `BiomeFilter = "snow"` — `⚠ 1` on row, badge next to BiomeFilter row in detail with the message in tooltip.
- Set Pool to anything but Mandatory — badge next to Pool dropdown.
- Set MinCount=1 MaxCount=3 — badge next to count row.
- DevTools console clean.

**Step 6: Commit**

```bash
git add src/OldenEra.Web/Components/ZoneContent/
git commit -m "feat(web): inline validator warning badges for zone-content

Aggregated row badge plus per-field badges in detail. Reuses
ZoneContentEmitWarnings via ZoneContentWarningProjection; UI
maps each warning code to the offending control."
```

---

## Final pass: fresh-eyes review

Before declaring merge-ready:

1. Run all tests: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` and `dotnet test tests/OldenEra.Web.Tests/OldenEra.Web.Tests.csproj`. (TemplateEditor.Tests if it builds on Mac.)
2. Build the web project. CI builds the WPF host and the solution; rely on it.
3. Run the dev server one more time and walk every tab end to end. Especially: Per-zone letter add → switch letter → return → no data loss; defaults-compare toggle → preview reflects defaults; preset insertion → row matches preset values.
4. Use `superpowers:requesting-code-review` for a final reviewer pass.
5. Push branch, open PR with `gh pr create --repo rannes/Olden-Era---Template-Generator`.

PR description template:

```
## Summary
- Web UI for zone-content (Round 4)
- Tabbed master+detail under the experimental toggle
- Row-level preset insertion
- Defaults-compare toggle
- Inline validator warning badges
- WPF port deferred to a follow-up round

## Test plan
- [ ] dotnet test tests/OldenEra.Generator.Tests passes
- [ ] dotnet test tests/OldenEra.Web.Tests passes
- [ ] Manual browser verification per Task 7 step 5
- [ ] CI green on Mac runner (WPF build + solution build)
```

---

## Notes for the executing agent

- After Task 1 lands, ask the user whether to continue dispatching subagents unattended (default per Round 3) or check in per-task.
- **Mac-only constraint:** the WPF host (`OldenEra.TemplateEditor`) and the solution file (`OldenEra.slnx`) do not build on Mac. Don't try. CI covers them.
- **Stale-fingerprint trap:** always `dotnet watch run`, never `dotnet run`, for the Web project.
- **Blazor errors:** the on-page error UI swallows stack traces. DevTools console is the source of truth.
- **WPF Style trap:** `Style.Triggers` must come after all `Setter`s; BAML errors only show up in CI on Mac. Doesn't apply to this round (Web only) but flagged for reviewer awareness.
- **Round 3 follow-ups** are deferred unless touched: `IsDefault` zero-vs-default fix, `SettingsFileJsonOptions` factory extraction.
