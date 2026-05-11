# Customizable Zone Content Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Land the all-in-one MR for customizable zone content (player + neutral + connection rules) across the shared library, both hosts, share codec, and experimental gate, behind the `ExperimentalFeaturesEnabled` flag.

**Architecture:** Library-first. New `OldenEra.Generator/Services/ZoneContent/` namespace owns resolve/emit/validate/presets, backed by existing `GameDataCatalog`/`KnownIds`/`CommunityCatalog`. `TemplateGenerator` checks user lists and falls back to today's hard-coded behaviour when empty (zero-regression guarantee). Share codec stays at v1 with additive optional fields. Both hosts get a new top-level "Zone Content" panel with three tabs (Player / Neutral / Connection Rules) using the master-list + detail-panel pattern.

**Tech Stack:** C# 12 / .NET 10, Blazor WebAssembly, WPF (`net10.0-windows`), xUnit, SkiaSharp (unrelated here, mentioned for completeness).

**Companion design:** `docs/plans/2026-05-11-customizable-zone-content-design.md`. Read it before starting.

**Build & test commands:**

```bash
dotnet build OldenEra.slnx
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
# WPF host only builds on Windows; on macOS rely on the library + Generator.Tests + Web build:
dotnet build src/OldenEra.Web/OldenEra.Web.csproj
```

**TDD discipline:** every behaviour-bearing task starts with a failing test. UI scaffolding tasks (XAML / Razor wiring) are exempt; their behaviour is exercised through the SettingsMapper / share-codec round-trip tests already on the library side.

---

## Phase 1 — Library data model

### Task 1.1: Add `ZoneContentPool` and `NeutralZoneTier` enums

**Files:**
- Create: `src/OldenEra.Generator/Models/Generator/ZoneContentEnums.cs`

**Step 1: Write the file**

```csharp
namespace OldenEra.Generator.Models
{
    public enum ZoneContentPool
    {
        Mandatory,
        Guarded,
        Unguarded,
        Resources
    }

    public enum NeutralZoneTier
    {
        Poor,
        Normal,
        Rich
    }
}
```

**Step 2: Build**

Run: `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj`
Expected: build succeeds.

**Step 3: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/ZoneContentEnums.cs
git commit -m "feat(library): add ZoneContentPool and NeutralZoneTier enums"
```

---

### Task 1.2: Add `ContentItem` DTO with default-instance equality test

**Files:**
- Create: `src/OldenEra.Generator/Models/Generator/ContentItem.cs`
- Create: `tests/OldenEra.Generator.Tests/ContentItemTests.cs`

**Step 1: Write the failing test**

```csharp
using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ContentItemTests
{
    [Fact]
    public void Defaults_match_design_spec()
    {
        var item = new ContentItem();
        Assert.Equal("", item.Sid);
        Assert.False(item.IsGroup);
        Assert.Equal(1, item.MinCount);
        Assert.Equal(1, item.MaxCount);
        Assert.Equal(ZoneContentPool.Mandatory, item.Pool);
        Assert.False(item.IsGuarded);
        Assert.False(item.NearCastle);
        Assert.Null(item.RoadDistance);
        Assert.Empty(item.FactionAffinity);
        Assert.Empty(item.BiomeFilter);
    }
}
```

**Step 2: Run, verify FAIL**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter ContentItemTests`
Expected: FAIL — `ContentItem` not defined.

**Step 3: Implement**

```csharp
using System.Collections.Generic;

namespace OldenEra.Generator.Models
{
    public sealed class ContentItem
    {
        public string Sid { get; set; } = "";
        public bool IsGroup { get; set; }
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; } = 1;
        public ZoneContentPool Pool { get; set; } = ZoneContentPool.Mandatory;
        public bool IsGuarded { get; set; }
        public bool NearCastle { get; set; }
        public string? RoadDistance { get; set; }
        public List<string> FactionAffinity { get; set; } = new();
        public List<string> BiomeFilter { get; set; } = new();
    }
}
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/ContentItem.cs tests/OldenEra.Generator.Tests/ContentItemTests.cs
git commit -m "feat(library): add ContentItem DTO"
```

---

### Task 1.3: Add `ZoneContentList`, `NeutralZoneContent`, `ContentConnectionRule` DTOs

**Files:**
- Create: `src/OldenEra.Generator/Models/Generator/ZoneContentList.cs`
- Create: `src/OldenEra.Generator/Models/Generator/NeutralZoneContent.cs`
- Create: `src/OldenEra.Generator/Models/Generator/ContentConnectionRule.cs`
- Modify: `tests/OldenEra.Generator.Tests/ContentItemTests.cs` — append container default tests.

**Step 1: Failing test (append to ContentItemTests)**

```csharp
[Fact]
public void NeutralZoneContent_defaults_are_empty_collections()
{
    var n = new NeutralZoneContent();
    Assert.Empty(n.Global.Items);
    Assert.Empty(n.ByTier);
    Assert.Empty(n.ByZoneLetter);
}

[Fact]
public void ContentConnectionRule_defaults()
{
    var r = new ContentConnectionRule();
    Assert.Equal(ContentRuleType.Distance, r.Type);
    Assert.Equal("", r.FromRef);
    Assert.Equal("", r.ToRef);
    Assert.Null(r.RoadType);
    Assert.Null(r.MinDistance);
    Assert.Null(r.MaxDistance);
}
```

**Step 2: Run, verify FAIL**

**Step 3: Implement the three DTOs**

`ZoneContentList.cs`:
```csharp
using System.Collections.Generic;
namespace OldenEra.Generator.Models
{
    public sealed class ZoneContentList
    {
        public List<ContentItem> Items { get; set; } = new();
    }
}
```

`NeutralZoneContent.cs`:
```csharp
using System.Collections.Generic;
namespace OldenEra.Generator.Models
{
    public sealed class NeutralZoneContent
    {
        public ZoneContentList Global { get; set; } = new();
        public Dictionary<NeutralZoneTier, ZoneContentList> ByTier { get; set; } = new();
        public Dictionary<string, ZoneContentList> ByZoneLetter { get; set; } = new();
    }
}
```

`ContentConnectionRule.cs`:
```csharp
namespace OldenEra.Generator.Models
{
    public enum ContentRuleType { Distance, OnRoad, Between }

    public sealed class ContentConnectionRule
    {
        public ContentRuleType Type { get; set; } = ContentRuleType.Distance;
        public string FromRef { get; set; } = "";
        public string ToRef { get; set; } = "";
        public string? RoadType { get; set; }
        public double? MinDistance { get; set; }
        public double? MaxDistance { get; set; }
    }
}
```

**Step 4: Run tests, verify PASS**

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/ZoneContentList.cs \
        src/OldenEra.Generator/Models/Generator/NeutralZoneContent.cs \
        src/OldenEra.Generator/Models/Generator/ContentConnectionRule.cs \
        tests/OldenEra.Generator.Tests/ContentItemTests.cs
git commit -m "feat(library): add ZoneContentList, NeutralZoneContent, ContentConnectionRule"
```

---

### Task 1.4: Wire the three fields into `GeneratorSettings`

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs:175-219` — add three properties.
- Create: `tests/OldenEra.Generator.Tests/GeneratorSettingsContentTests.cs`

**Step 1: Failing test**

```csharp
using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class GeneratorSettingsContentTests
{
    [Fact]
    public void New_settings_have_empty_zone_content_lists()
    {
        var s = new GeneratorSettings();
        Assert.NotNull(s.PlayerZoneContent);
        Assert.Empty(s.PlayerZoneContent.Items);
        Assert.NotNull(s.NeutralZoneContent);
        Assert.Empty(s.NeutralZoneContent.Global.Items);
        Assert.NotNull(s.ContentConnectionRules);
        Assert.Empty(s.ContentConnectionRules);
    }
}
```

**Step 2: Run, verify FAIL**

**Step 3: Add the three properties on `GeneratorSettings`**

In `GeneratorSettings.cs`, just before the closing brace of the class, after `Bonuses`:

```csharp
        // ── Zone content (experimental) ─────────────────────────────────────────
        public ZoneContentList PlayerZoneContent { get; set; } = new();
        public NeutralZoneContent NeutralZoneContent { get; set; } = new();
        public List<ContentConnectionRule> ContentConnectionRules { get; set; } = new();
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs \
        tests/OldenEra.Generator.Tests/GeneratorSettingsContentTests.cs
git commit -m "feat(library): expose PlayerZoneContent, NeutralZoneContent, ContentConnectionRules on GeneratorSettings"
```

---

## Phase 2 — Resolver and validator (no generator changes yet)

### Task 2.1: `ZoneContentResolver` — empty inputs return empty

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentResolver.cs`
- Create: `tests/OldenEra.Generator.Tests/ZoneContentResolverTests.cs`

**Step 1: Failing test**

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentResolverTests
{
    [Fact]
    public void Empty_NeutralZoneContent_resolves_to_empty_list()
    {
        var cfg = new NeutralZoneContent();
        var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Red-A");
        Assert.Empty(resolved.Items);
    }
}
```

**Step 2: Run, verify FAIL** (type doesn't exist).

**Step 3: Implement**

```csharp
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneContentResolver
    {
        public static ZoneContentList Resolve(
            NeutralZoneContent cfg,
            NeutralZoneTier tier,
            string zoneLetter)
        {
            var result = new ZoneContentList();
            return result;
        }
    }
}
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Services/ZoneContent/ZoneContentResolver.cs \
        tests/OldenEra.Generator.Tests/ZoneContentResolverTests.cs
git commit -m "feat(library): scaffold ZoneContentResolver with empty-config behaviour"
```

---

### Task 2.2: Resolver — Global merges through to output

**Files:**
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentResolver.cs`
- Modify: `tests/OldenEra.Generator.Tests/ZoneContentResolverTests.cs`

**Step 1: Failing test (append)**

```csharp
[Fact]
public void Global_items_appear_in_resolved_output()
{
    var cfg = new NeutralZoneContent();
    cfg.Global.Items.Add(new ContentItem { Sid = "name_mana_well" });
    var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Red-A");
    Assert.Single(resolved.Items);
    Assert.Equal("name_mana_well", resolved.Items[0].Sid);
}
```

**Step 2: Run, verify FAIL**

**Step 3: Implement**

In `ZoneContentResolver.Resolve`:
```csharp
foreach (var item in cfg.Global.Items)
    result.Items.Add(item);
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git commit -am "feat(library): resolver passes Global items through"
```

---

### Task 2.3: Resolver — Tier overrides append; same-Sid replaces

**Files:**
- Modify: resolver + test file.

**Step 1: Failing tests (append)**

```csharp
[Fact]
public void Tier_items_append_to_global()
{
    var cfg = new NeutralZoneContent();
    cfg.Global.Items.Add(new ContentItem { Sid = "name_mana_well" });
    cfg.ByTier[NeutralZoneTier.Rich] = new ZoneContentList();
    cfg.ByTier[NeutralZoneTier.Rich].Items.Add(new ContentItem { Sid = "name_pandora_box_army" });
    var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Rich, "Red-A");
    Assert.Equal(2, resolved.Items.Count);
    Assert.Contains(resolved.Items, i => i.Sid == "name_pandora_box_army");
}

[Fact]
public void Tier_replaces_same_Sid_from_global()
{
    var cfg = new NeutralZoneContent();
    cfg.Global.Items.Add(new ContentItem { Sid = "name_mana_well", MaxCount = 1 });
    cfg.ByTier[NeutralZoneTier.Rich] = new ZoneContentList();
    cfg.ByTier[NeutralZoneTier.Rich].Items.Add(new ContentItem { Sid = "name_mana_well", MaxCount = 4 });
    var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Rich, "Red-A");
    Assert.Single(resolved.Items);
    Assert.Equal(4, resolved.Items[0].MaxCount);
}

[Fact]
public void Other_tier_does_not_apply()
{
    var cfg = new NeutralZoneContent();
    cfg.ByTier[NeutralZoneTier.Rich] = new ZoneContentList();
    cfg.ByTier[NeutralZoneTier.Rich].Items.Add(new ContentItem { Sid = "name_pandora_box_army" });
    var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Poor, "Red-A");
    Assert.Empty(resolved.Items);
}
```

**Step 2: Run, verify FAIL**

**Step 3: Implement merge**

Replace resolver body with:

```csharp
public static ZoneContentList Resolve(NeutralZoneContent cfg, NeutralZoneTier tier, string zoneLetter)
{
    var byKey = new Dictionary<string, ContentItem>(StringComparer.Ordinal);
    var order = new List<string>();

    void Apply(IEnumerable<ContentItem>? items)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (byKey.ContainsKey(item.Sid))
            {
                byKey[item.Sid] = item;
            }
            else
            {
                byKey[item.Sid] = item;
                order.Add(item.Sid);
            }
        }
    }

    Apply(cfg.Global.Items);
    if (cfg.ByTier.TryGetValue(tier, out var tierList))
        Apply(tierList.Items);
    if (cfg.ByZoneLetter.TryGetValue(zoneLetter, out var letterList))
        Apply(letterList.Items);

    var result = new ZoneContentList();
    foreach (var sid in order)
        result.Items.Add(byKey[sid]);
    return result;
}
```

Add `using System;` and `using System.Collections.Generic;` at the top.

**Step 4: Run all `ZoneContentResolverTests`, verify PASS**

**Step 5: Commit**

```bash
git commit -am "feat(library): resolver merges Global → Tier with same-Sid replacement"
```

---

### Task 2.4: Resolver — ByZoneLetter overrides

**Files:**
- Modify: test only (logic already implemented in 2.3).

**Step 1: Add test**

```csharp
[Fact]
public void Letter_replaces_tier_for_that_zone()
{
    var cfg = new NeutralZoneContent();
    cfg.ByTier[NeutralZoneTier.Normal] = new ZoneContentList();
    cfg.ByTier[NeutralZoneTier.Normal].Items.Add(new ContentItem { Sid = "name_mana_well", MaxCount = 1 });
    cfg.ByZoneLetter["Red-A"] = new ZoneContentList();
    cfg.ByZoneLetter["Red-A"].Items.Add(new ContentItem { Sid = "name_mana_well", MaxCount = 7 });
    var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Red-A");
    Assert.Single(resolved.Items);
    Assert.Equal(7, resolved.Items[0].MaxCount);
}

[Fact]
public void Letter_only_applies_to_that_letter()
{
    var cfg = new NeutralZoneContent();
    cfg.ByZoneLetter["Red-A"] = new ZoneContentList();
    cfg.ByZoneLetter["Red-A"].Items.Add(new ContentItem { Sid = "name_mana_well" });
    var resolved = ZoneContentResolver.Resolve(cfg, NeutralZoneTier.Normal, "Orange-A");
    Assert.Empty(resolved.Items);
}
```

**Step 2: Run, verify PASS** (no new code).

**Step 3: Commit**

```bash
git commit -am "test(library): pin ByZoneLetter override semantics"
```

---

### Task 2.5: `ContentItemValidator` — invalid SID, count ranges, contradictions

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ContentItemValidator.cs`
- Create: `tests/OldenEra.Generator.Tests/ContentItemValidatorTests.cs`

**Step 1: Failing tests**

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ContentItemValidatorTests
{
    [Fact]
    public void Default_item_is_valid_except_empty_sid()
    {
        var item = new ContentItem();
        var issues = ContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("Sid", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Item_with_max_less_than_min_fails()
    {
        var item = new ContentItem { Sid = "name_mana_well", MinCount = 3, MaxCount = 1 };
        var issues = ContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("MaxCount"));
    }

    [Fact]
    public void Item_with_negative_count_fails()
    {
        var item = new ContentItem { Sid = "x", MinCount = -1, MaxCount = 1 };
        var issues = ContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("MinCount"));
    }

    [Fact]
    public void Healthy_item_has_no_issues()
    {
        var item = new ContentItem { Sid = "name_mana_well", MinCount = 1, MaxCount = 3 };
        Assert.Empty(ContentItemValidator.Validate(item));
    }

    [Fact]
    public void NearCastle_with_far_road_distance_warns()
    {
        var item = new ContentItem
        {
            Sid = "name_mana_well",
            NearCastle = true,
            RoadDistance = "Far"
        };
        var issues = ContentItemValidator.Validate(item);
        Assert.Contains(issues, i => i.Contains("NearCastle") && i.Contains("Far"));
    }
}
```

**Step 2: Run, verify FAIL**

**Step 3: Implement**

```csharp
using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ContentItemValidator
    {
        public static IReadOnlyList<string> Validate(ContentItem item)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(item.Sid))
                issues.Add("Sid must be non-empty.");

            if (item.MinCount < 0)
                issues.Add("MinCount must be >= 0.");

            if (item.MaxCount < item.MinCount)
                issues.Add($"MaxCount ({item.MaxCount}) must be >= MinCount ({item.MinCount}).");

            if (item.NearCastle && item.RoadDistance == "Far")
                issues.Add("NearCastle is incompatible with RoadDistance=Far.");

            return issues;
        }
    }
}
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Services/ZoneContent/ContentItemValidator.cs \
        tests/OldenEra.Generator.Tests/ContentItemValidatorTests.cs
git commit -m "feat(library): add ContentItemValidator"
```

---

### Task 2.6: `ZoneContentSidCatalog` — joined picker source

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentSidCatalog.cs`
- Create: `tests/OldenEra.Generator.Tests/ZoneContentSidCatalogTests.cs`

**Step 1: Inspect existing catalogs**

Read first ~50 lines each of `GameDataCatalog.cs` and `CommunityCatalog.cs` to see what they expose. The picker just needs `(sid, friendlyName, category)` triples.

**Step 2: Failing test**

```csharp
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentSidCatalogTests
{
    [Fact]
    public void Catalog_contains_known_mana_well_sid()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.Contains(entries, e => e.Sid == "name_mana_well");
    }

    [Fact]
    public void Each_entry_has_friendly_name()
    {
        var entries = ZoneContentSidCatalog.All();
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.FriendlyName)));
    }
}
```

**Step 3: Run, verify FAIL**

**Step 4: Implement**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record ZoneContentSidEntry(string Sid, string FriendlyName, string Category);

    public static class ZoneContentSidCatalog
    {
        // Initial list seeds the picker; later we union with GameDataCatalog +
        // CommunityCatalog. Adding entries here is safe — the validator only
        // warns on missing SIDs, never blocks.
        private static readonly ZoneContentSidEntry[] _seed =
        {
            new("name_mana_well", "Mana Well", "Mandatory"),
            new("name_pandora_box_army", "Pandora's Box (Army)", "Mandatory"),
            new("name_pandora_box_resources", "Pandora's Box (Resources)", "Mandatory"),
            new("name_pandora_box_xp", "Pandora's Box (XP)", "Mandatory"),
        };

        public static IReadOnlyList<ZoneContentSidEntry> All() => _seed;
    }
}
```

(We expand later from `GameDataCatalog` once we pin a public surface; seeded list is enough to unblock the picker.)

**Step 5: Run, verify PASS**

**Step 6: Commit**

```bash
git add src/OldenEra.Generator/Services/ZoneContent/ZoneContentSidCatalog.cs \
        tests/OldenEra.Generator.Tests/ZoneContentSidCatalogTests.cs
git commit -m "feat(library): seed ZoneContentSidCatalog with curated picker entries"
```

---

### Task 2.7: `ContentPresets` — built-in curated rows

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ContentPresets.cs`
- Create: `tests/OldenEra.Generator.Tests/ContentPresetsTests.cs`

**Step 1: Failing test**

```csharp
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ContentPresetsTests
{
    [Fact]
    public void Presets_are_nonempty_and_valid()
    {
        var presets = ContentPresets.All();
        Assert.NotEmpty(presets);
        Assert.All(presets, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.NotNull(p.Item);
            Assert.Empty(ContentItemValidator.Validate(p.Item));
        });
    }
}
```

**Step 2: Run, verify FAIL**

**Step 3: Implement**

```csharp
using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record ContentPreset(string Name, ContentItem Item);

    public static class ContentPresets
    {
        public static IReadOnlyList<ContentPreset> All() => new ContentPreset[]
        {
            new("Mana Well x1 (guarded)", new ContentItem
            {
                Sid = "name_mana_well", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true,
            }),
            new("Pandora Army x1 (guarded, near castle)", new ContentItem
            {
                Sid = "name_pandora_box_army", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true, NearCastle = true,
            }),
            new("Pandora Resources x1", new ContentItem
            {
                Sid = "name_pandora_box_resources", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Pandora XP x1", new ContentItem
            {
                Sid = "name_pandora_box_xp", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
        };
    }
}
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Services/ZoneContent/ContentPresets.cs \
        tests/OldenEra.Generator.Tests/ContentPresetsTests.cs
git commit -m "feat(library): add built-in ContentPresets"
```

---

## Phase 3 — Emitter (no generator wiring yet)

### Task 3.1: `ZoneContentEmitter.Apply` — Mandatory pool routing

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentEmitter.cs`
- Create: `tests/OldenEra.Generator.Tests/ZoneContentEmitterTests.cs`

**Step 1: Inspect `Zone` shape**

Open `src/OldenEra.Generator/Models/Unfrozen/` to identify the `Zone` class fields: `MandatoryContent`, `GuardedContentPool`, `UnguardedContentPool`, `ResourcesContentPool`. Note exact types.

**Step 2: Failing test**

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

public class ZoneContentEmitterTests
{
    [Fact]
    public void Mandatory_pool_appends_sid_to_MandatoryContent()
    {
        var zone = new Zone();
        var list = new ZoneContentList();
        list.Items.Add(new ContentItem
        {
            Sid = "name_mana_well",
            Pool = ZoneContentPool.Mandatory,
            MinCount = 1, MaxCount = 1,
        });
        ZoneContentEmitter.Apply(zone, list);
        Assert.Contains("name_mana_well", zone.MandatoryContent);
    }
}
```

**Step 3: Run, verify FAIL**

**Step 4: Implement minimal**

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneContentEmitter
    {
        public static void Apply(Zone zone, ZoneContentList list)
        {
            foreach (var item in list.Items)
            {
                switch (item.Pool)
                {
                    case ZoneContentPool.Mandatory:
                        zone.MandatoryContent.Add(item.Sid);
                        break;
                    // Other pools handled in subsequent tasks.
                }
            }
        }
    }
}
```

If `MandatoryContent` is `List<string>?` confirm initialization (look at `Zone` defaults). Initialize via `zone.MandatoryContent ??= new();` if nullable.

**Step 5: Run, verify PASS**

**Step 6: Commit**

```bash
git add src/OldenEra.Generator/Services/ZoneContent/ZoneContentEmitter.cs \
        tests/OldenEra.Generator.Tests/ZoneContentEmitterTests.cs
git commit -m "feat(library): emit Mandatory pool entries to Zone.MandatoryContent"
```

---

### Task 3.2: Emitter — Guarded / Unguarded / Resources pool routing

**Files:**
- Modify: emitter + test.

**Step 1: Failing tests (one per pool)**

```csharp
[Theory]
[InlineData(ZoneContentPool.Guarded)]
[InlineData(ZoneContentPool.Unguarded)]
[InlineData(ZoneContentPool.Resources)]
public void Pool_routes_to_corresponding_zone_field(ZoneContentPool pool)
{
    var zone = new Zone();
    var list = new ZoneContentList();
    list.Items.Add(new ContentItem { Sid = "name_x", Pool = pool });
    ZoneContentEmitter.Apply(zone, list);

    var target = pool switch
    {
        ZoneContentPool.Guarded => zone.GuardedContentPool,
        ZoneContentPool.Unguarded => zone.UnguardedContentPool,
        ZoneContentPool.Resources => zone.ResourcesContentPool,
        _ => null,
    };
    Assert.NotNull(target);
    Assert.Contains("name_x", target!);
}
```

(If a pool field on `Zone` is a list of structured objects rather than strings, adapt: emitter wraps the `Sid` in the right shape; tests assert on the structured equivalent.)

**Step 2: Run, verify FAIL**

**Step 3: Extend the switch in emitter**

```csharp
case ZoneContentPool.Guarded:
    (zone.GuardedContentPool ??= new()).Add(item.Sid);
    break;
case ZoneContentPool.Unguarded:
    (zone.UnguardedContentPool ??= new()).Add(item.Sid);
    break;
case ZoneContentPool.Resources:
    (zone.ResourcesContentPool ??= new()).Add(item.Sid);
    break;
```

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git commit -am "feat(library): emit Guarded/Unguarded/Resources pool entries"
```

---

### Task 3.3: Emitter — `MATCH_PLAYER` faction sentinel and `FromList`

(See design doc for sentinel rules. Tests assert on the structured shape produced — pin it against an example in `Sprint.rmg.json` you read first.)

**Step 1: Read `Sprint.rmg.json:200-260`** to confirm the exact shape of `Match` and `FromList` selectors used on `MainObjects` / `MandatoryContent`.

**Step 2: Failing test**

```csharp
[Fact]
public void Match_player_sentinel_emits_Match_selector()
{
    var zone = new Zone();
    var list = new ZoneContentList();
    list.Items.Add(new ContentItem
    {
        Sid = "name_mana_well",
        FactionAffinity = new() { "MATCH_PLAYER" },
    });
    ZoneContentEmitter.Apply(zone, list);
    // Assert against whichever structured object the emitter produces.
    // Adapt to the actual Zone schema once Step 1 is read.
}
```

**Step 3: Implement faction-affinity translation**

If `MandatoryContent` is just a list of strings, faction affinity has no place to live and we must emit it onto the `MainObjects` slot or a richer `MandatoryContent` element type. **Likely path:** add a private helper `EmitMandatoryEntry(Zone, ContentItem)` that writes a structured entry per `MainObjects.Faction` shape we discovered. Mirror for `FromList`.

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git commit -am "feat(library): emit faction affinity (Match player + FromList)"
```

---

### Task 3.4: Emitter — biome filter, count range, road distance, near-castle, guarded

Repeat the failing-test → minimal-impl → pass cycle for each remaining knob. Each gets one test that asserts the field on the produced `Zone` element matches the spec.

Order (one task per bullet, one commit per task):

- 3.4a Biome filter
- 3.4b Min/Max count range → `Random` typed selector
- 3.4c RoadDistance
- 3.4d NearCastle
- 3.4e IsGuarded

If any knob has no `Zone` slot to live in (no schema support), emit a TODO comment + log a warning, **and** add a unit test asserting the warning was emitted. Do not silently drop user input.

---

### Task 3.5: `ConnectionRuleEmitter` — write rules into `Zone.Connections`

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ConnectionRuleEmitter.cs`
- Create: `tests/OldenEra.Generator.Tests/ConnectionRuleEmitterTests.cs`

**Step 1: Read `Sprint.rmg.json:227` and surrounding 15 lines** to pin the exact rule shape.

**Step 2: Failing test**

```csharp
[Fact]
public void Distance_rule_emits_to_target_zone_connections()
{
    var template = new RmgTemplate(); // the top-level DTO
    // ... seed two zones with a connection
    var rules = new[]
    {
        new ContentConnectionRule
        {
            Type = ContentRuleType.Distance,
            FromRef = "Connection:Spawn-A-Red-A",
            ToRef = "MandatoryContent:name_mana_well",
            RoadType = "Stone",
        }
    };
    ConnectionRuleEmitter.Apply(template, rules);
    // Assert the connection now contains a Stone rule with from/to matching the refs.
}
```

**Step 3: Implement minimal**

The implementation pattern: parse `FromRef` / `ToRef` prefixes (`Connection:`, `MandatoryContent:`, `Item:`), find the matching connection, append a rule object whose JSON shape matches the reference template.

**Step 4: Run, verify PASS**

**Step 5: Commit**

```bash
git commit -am "feat(library): emit ContentConnectionRule into template Connections"
```

---

## Phase 4 — Generator wiring (with empty-list fallback)

### Task 4.1: Snapshot test pinning Jebus Cross with empty user lists

**Files:**
- Create: `tests/OldenEra.Generator.Tests/ZoneContentEmptyFallbackTests.cs`

**Step 1: Test**

```csharp
[Fact]
public void Empty_user_lists_produce_byte_identical_template_to_baseline()
{
    var settingsEmpty = JebusCrossPreset(); // existing helper, or build inline
    var jsonNew = TemplateGenerator.GenerateJson(settingsEmpty, seed: 12345);

    // Baseline: regenerate without the new code path. Easiest: read a
    // fixture committed alongside this test, generated once at the start
    // of phase 4 with empty content lists. If the fixture is missing,
    // fail with a clear message instructing how to regenerate.
    var baseline = File.ReadAllText("Fixtures/jebus-empty-content-baseline.json");
    Assert.Equal(baseline, jsonNew);
}
```

**Step 2: Generate the baseline** by running the generator against the current code (before Task 4.2 wires in the new path) and committing the file under `tests/OldenEra.Generator.Tests/Fixtures/jebus-empty-content-baseline.json`.

**Step 3: Run, verify PASS** (current code is the baseline).

**Step 4: Commit**

```bash
git add tests/OldenEra.Generator.Tests/ZoneContentEmptyFallbackTests.cs \
        tests/OldenEra.Generator.Tests/Fixtures/jebus-empty-content-baseline.json
git commit -m "test(library): pin empty-content-list Jebus Cross baseline"
```

---

### Task 4.2: Wire emitter into `BuildSpawnZone` with empty fallback

**Files:**
- Modify: `src/OldenEra.Generator/Services/TemplateGenerator.cs:2230-2297` (BuildSpawnZone region).

**Step 1: Add the conditional**

After zone construction but before `return zone`:

```csharp
if (settings.PlayerZoneContent.Items.Count > 0)
{
    ZoneContentEmitter.Apply(zone, settings.PlayerZoneContent);
}
// else: leave zone.MandatoryContent / pools as today's hard-coded defaults.
```

`BuildSpawnZone` is currently `static`; add a `GeneratorSettings settings` parameter and thread it through from the caller.

**Step 2: Run all `OldenEra.Generator.Tests`**

Expected: empty-fallback test from 4.1 still PASS (because `PlayerZoneContent` is empty in that fixture). All other tests still PASS.

**Step 3: Commit**

```bash
git commit -am "feat(generator): wire PlayerZoneContent through BuildSpawnZone with empty fallback"
```

---

### Task 4.3: Wire `BuildNeutralZone`

Same pattern, calling `ZoneContentResolver.Resolve(settings.NeutralZoneContent, plan.Quality, plan.Letter)`. Run tests, expect baseline still PASS.

Map `NeutralZoneQuality → NeutralZoneTier` via a private `ToTier` helper in `TemplateGenerator`.

```bash
git commit -m "feat(generator): wire NeutralZoneContent through BuildNeutralZone"
```

---

### Task 4.4: Apply `ContentConnectionRules` after template assembly

In `TemplateGenerator.Generate()` (top-level), after the template is fully built but before serialization:

```csharp
if (settings.ContentConnectionRules.Count > 0)
    ConnectionRuleEmitter.Apply(template, settings.ContentConnectionRules);
```

Tests: baseline still PASS; add a small dedicated test that with one rule the output JSON contains the corresponding rule.

```bash
git commit -m "feat(generator): apply ContentConnectionRules after assembly"
```

---

### Task 4.5: Filled-list end-to-end test

**Files:**
- Create: `tests/OldenEra.Generator.Tests/ZoneContentEndToEndTests.cs`

Build a `GeneratorSettings` with one `PlayerZoneContent` row, one `NeutralZoneContent.Global` row, one `ContentConnectionRule`. Generate JSON, deserialize, assert the items appear on at least one zone and the rule appears on at least one connection.

```bash
git commit -m "test(generator): end-to-end coverage for populated zone content"
```

---

## Phase 5 — Settings persistence and share codec

### Task 5.1: `SettingsFile` mirror

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/SettingsFile.cs` — add three optional fields:

```csharp
[JsonPropertyName("playerZoneContent")] public ZoneContentList? PlayerZoneContent { get; set; }
[JsonPropertyName("neutralZoneContent")] public NeutralZoneContent? NeutralZoneContent { get; set; }
[JsonPropertyName("contentConnectionRules")] public List<ContentConnectionRule>? ContentConnectionRules { get; set; }
```

**Step 2: Test**

In `tests/.../SettingsFileSeedTests.cs` style, add:

```csharp
[Fact]
public void Empty_settings_file_does_not_emit_zone_content_keys()
{
    var sf = new SettingsFile();
    var json = JsonSerializer.Serialize(sf, JsonOpts());
    Assert.DoesNotContain("playerZoneContent", json);
}
```

(Use `DefaultIgnoreCondition = WhenWritingNull` already configured for the existing settings file.)

**Step 3: Commit**

```bash
git commit -am "feat(library): mirror zone content fields on SettingsFile"
```

---

### Task 5.2: `SettingsMapper` round-trippers

**Files:**
- Modify: `src/OldenEra.Generator/Services/SettingsMapper.cs`
- Create: `tests/OldenEra.Generator.Tests/SettingsMapperZoneContentTests.cs`

Three new methods: `MapPlayerZoneContent`, `MapNeutralZoneContent`, `MapContentConnectionRules` (each `GeneratorSettings ↔ SettingsFile`).

**Step 1: Failing test**

```csharp
[Fact]
public void Round_trip_of_player_zone_content_preserves_all_knobs()
{
    var src = new GeneratorSettings();
    src.PlayerZoneContent.Items.Add(new ContentItem
    {
        Sid = "name_mana_well",
        MinCount = 2, MaxCount = 5,
        Pool = ZoneContentPool.Guarded,
        IsGuarded = true, NearCastle = true,
        RoadDistance = "Mid",
        FactionAffinity = { "necropolis", "MATCH_PLAYER" },
        BiomeFilter = { "snow" },
        IsGroup = true,
    });

    var file = SettingsMapper.ToFile(src);
    var dst = SettingsMapper.FromFile(file);

    var item = Assert.Single(dst.PlayerZoneContent.Items);
    Assert.Equal("name_mana_well", item.Sid);
    Assert.Equal(2, item.MinCount);
    Assert.Equal(5, item.MaxCount);
    Assert.Equal(ZoneContentPool.Guarded, item.Pool);
    Assert.True(item.IsGuarded);
    Assert.True(item.NearCastle);
    Assert.Equal("Mid", item.RoadDistance);
    Assert.Equal(new[] { "necropolis", "MATCH_PLAYER" }, item.FactionAffinity);
    Assert.Equal(new[] { "snow" }, item.BiomeFilter);
    Assert.True(item.IsGroup);
}
```

Mirror tests for `NeutralZoneContent` (Global + tier + zone letter) and `ContentConnectionRules`.

**Step 2: Implement mapper methods**

**Step 3: Run, verify PASS**

**Step 4: Commit**

```bash
git commit -am "feat(library): round-trip zone content through SettingsMapper"
```

---

### Task 5.3: Existing pinned v1 share-codec fixture still decodes

**Files:**
- Modify: `tests/OldenEra.Generator.Tests/SettingsShareCodecSeedTests.cs` (or create a sibling) — add an assertion that decoding the existing pinned payload yields empty zone-content lists (no exception).

```bash
git commit -am "test(share): pinned v1 payload decodes with empty zone content"
```

---

### Task 5.4: New pinned v1 share-codec fixture with populated zone content

**Files:**
- Create: `tests/OldenEra.Generator.Tests/Fixtures/share-codec-zone-content-v1.txt` containing the encoded base64url payload.
- Modify: `tests/OldenEra.Generator.Tests/SettingsShareCodecSeedTests.cs`

Procedure:
1. Build `GeneratorSettings` with one of each kind of entry populated.
2. `SettingsShareCodec.Encode(...)` → write the resulting string to the fixture file.
3. Add a test that decodes the fixture file and asserts equality to the source.

```bash
git commit -m "test(share): pin populated zone-content payload as forward-compat fixture"
```

---

## Phase 6 — Experimental gate plumbing

### Task 6.1: `ExperimentalFeaturesEnabled` already exists; pin its semantics for ZoneContent

**Files:**
- Modify: `src/OldenEra.Generator/Services/SettingsValidator.cs` (only if the validator should warn when zone content is non-empty but experimental is off). Otherwise: no library change — both hosts gate the *UI*; the generator honours non-empty lists regardless of the toggle (because share-link import should not silently lose data when the importing user has experimental off).

**Decision:** generator emits whatever is in settings; UI hides the panel under experimental, and import preserves data through round-trip. Document this in the design doc and skip a code change.

No commit.

---

## Phase 7 — Web host UI

### Task 7.1: Add `ZoneContent` route + nav entry

**Files:**
- Create: `src/OldenEra.Web/Pages/ZoneContent/ZoneContentPanel.razor`
- Create: `src/OldenEra.Web/Pages/ZoneContent/ZoneContentPanel.razor.css`
- Modify: `src/OldenEra.Web/Components/SectionNav.razor`
- Modify: `src/OldenEra.Web/Pages/Home.razor`

Skeleton page with three `<button>`-toggled tabs: Player, Neutral, Connection Rules. Hide the nav entry behind the experimental flag (read settings from the existing settings service used by `ExperimentalCard`).

```bash
git commit -m "feat(web): scaffold ZoneContent page and nav entry (experimental-gated)"
```

---

### Task 7.2: `PlayerZoneContent.razor` — master list + detail editor

Build the master list as `<ul class="content-list">` with summary chips per row, and a detail panel that two-way-binds every `ContentItem` field. Use existing UI patterns: spell-picker style (typeahead from `ZoneContentSidCatalog`), unit-ban-grid style for multi-selects.

Tasks (one per commit):
- 7.2a Render master list with stub items, no detail.
- 7.2b Add detail panel binding `Sid` (typeahead) and `MinCount`/`MaxCount`.
- 7.2c Wire `Pool`, `IsGuarded`, `NearCastle`, `RoadDistance`.
- 7.2d Wire `FactionAffinity` multi-select with "Match player" toggle.
- 7.2e Wire `BiomeFilter` multi-select.
- 7.2f Add buttons: Add (empty row), Add preset… (dropdown over `ContentPresets.All()`), Remove, Move up/down.
- 7.2g Add "Inspect defaults" — opens a read-only modal showing the hard-coded baseline as `ContentItem` rows; each has a "Copy to my list" button. (Defaults sourced from a static helper `DefaultZoneContentSnapshot.PlayerSpawn()` we add now in `OldenEra.Generator.Services.ZoneContent`.)

Each commit message: `feat(web): <thing>`.

---

### Task 7.3: `NeutralZoneContent.razor` — same UI plus scope picker

`<select>` at the top: `Global | Poor | Normal | Rich | <auto-listed zone letters from current topology>`. Reuse the master+detail component from 7.2 by extracting it into a child component `ContentItemListEditor.razor` (refactor in 7.2g).

Sub-tasks 7.3a..d.

---

### Task 7.4: `ConnectionRules.razor`

List of rules. Detail panel: `Type` enum, `From` ref picker, `To` ref picker (both two-step: kind selector + ref picker), `RoadType` enum, optional `MinDistance`/`MaxDistance`.

---

## Phase 8 — WPF host UI

Mirror the web host structure with WPF UserControls.

### Task 8.1: `Views/ZoneContentPanel.xaml(.cs)` skeleton with `TabControl`

Add nav entry to `MainWindow.xaml` (between Heroes and Win Conditions). Hide via the existing experimental-visibility binding.

```bash
git commit -m "feat(wpf): scaffold ZoneContentPanel"
```

### Task 8.2: `Views/PlayerZoneContentPanel.xaml(.cs)` master + detail

`ListBox` master, `Grid` detail. Sub-tasks mirror 7.2a..g.

### Task 8.3: `Views/NeutralZoneContentPanel.xaml(.cs)` with scope picker

### Task 8.4: `Views/ConnectionRulesPanel.xaml(.cs)`

Each panel commits its own. Use existing `ModernDarkTheme` resources; do not introduce new ones.

---

## Phase 9 — Documentation, UPSTREAM cursor, version bump

### Task 9.1: Update `UPSTREAM.md`

Mark `0c45c42`, `55a31a4`, `f47fae3`, `4a76ce2`, `edb64cb` as **ported** with the merge commit hash for this feature. Bump "Last reviewed upstream commit" if no newer upstream commits arrived.

### Task 9.2: Add `docs/features/zone-content.md`

Short user-facing doc: what the feature does, how the merge layers work, link to the design doc.

### Task 9.3: Bump version in `MainWindow.xaml.cs` (UpdateChecker constant) and the Web host version surface

Pattern from `b96e9f0 chore: document seed feature, bump version to v0.6.8`. Probably v0.7.0 given scope.

### Task 9.4: Final sweep

Run `dotnet build OldenEra.slnx` (or Web + library on macOS) and the full test suite. Ensure all green. Open the PR.

---

## Risks and what to do about them

- **Schema surprises.** The `Zone` DTO may not have a slot for one of the knobs (e.g. `BiomeFilter` per-item). When emitter Task 3.4 hits a missing slot, *do not silently drop the field*: emit a TODO + warning, write a unit test asserting the warning was emitted, and add a row to "Known limitations" in the user doc.
- **Connection-rule references go stale when topology changes.** The validator (Task 2.5 has room for extension) gets a future enhancement to validate refs against the active topology. For v1, document the limitation; rules referencing unknown SIDs / connection IDs become validation warnings, not errors.
- **JSON byte-equality fragility.** Phase 4's empty-fallback baseline is per-seed. Generate it with a fixed seed; pin that seed in the test. Re-baseline only when an *unrelated* generator change is intended.
- **All-in-one MR review burden.** Mitigated by phase-by-phase commits and snapshot tests in early phases that prove no regression before UI lands.

---

Plan complete and saved to `docs/plans/2026-05-11-customizable-zone-content-impl.md`. Two execution options:

1. **Subagent-Driven (this session)** — I dispatch a fresh subagent per task and review between tasks. Faster iteration; my main context stays clean.
2. **Parallel Session (separate)** — You open a new session in a worktree, that session runs `superpowers:executing-plans` against this file and works through the phases.

Which approach?
