# Zone Content Round 2 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Land the Round 2 emitter, DTO follow-ups, and validator extension in `OldenEra.Generator` so user-authored zone content reaches the Mandatory pool and user road decorations reach `Zone.Roads[]`, with empty inputs producing byte-identical output to today.

**Architecture:** Two pure-static emitters (`ZoneContentEmitter`, `ZoneRoadDecorationEmitter`) sharing a single warning inspector (`ZoneContentEmitWarnings`). DTO surface is renamed/retyped in one pass before the emitters are wired in. Wiring point for content is `BuildSpawnMandatoryContent` / `BuildNeutralMandatoryContent` (central group construction); wiring point for roads is `BuildSpawnZone` / `BuildNeutralZone`. Empty-list fast paths give the no-op guarantee.

**Tech Stack:** C# / .NET 10, xUnit, FluentAssertions (whatever the existing test files use). Mac dev: `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj` + `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`. WPF host is not touched.

**Reference:** `docs/plans/2026-05-12-zone-content-round-2-design.md` is the source of truth for decisions. `docs/plans/2026-05-12-zone-content-schema-research.md` is the schema reference for placement-rule ranges and shape.

---

## Working principles

- **TDD throughout** — failing test first, minimal implementation, green, commit.
- **One logical change per commit.** The plan is sliced so each commit leaves the build green.
- **Mac sanity** — after every code commit, run the targeted build + test commands above and confirm zero failures before moving on.
- **No host UI work.** If a task seems to require WPF or Blazor, stop and ask — Round 2 is library-only.

---

## Task 1: DTO — `RoadDistance` enum

**Files:**
- Create: `src/OldenEra.Generator/Models/Generator/RoadDistance.cs`
- Modify: `src/OldenEra.Generator/Models/Generator/ZoneContentItem.cs`
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentItemValidator.cs`
- Modify: any test file referencing `RoadDistance` as a string (`grep -rn "RoadDistance" tests`)

**Step 1.1 — Add the enum.**

Create `RoadDistance.cs`:

```csharp
namespace OldenEra.Generator.Models
{
    public enum RoadDistance
    {
        Close,
        Mid,
        Far,
    }
}
```

**Step 1.2 — Retype the property.**

In `ZoneContentItem.cs`:

```csharp
public RoadDistance? RoadDistance { get; set; }
```

(Replaces the existing `public string? RoadDistance { get; set; }`.)

**Step 1.3 — Simplify the validator.**

In `ZoneContentItemValidator.cs`, drop the string-canonicalization branch (the enum makes invalid values impossible). Keep the `NearCastle && RoadDistance == Far` cross-field rule — update the comparison to enum form:

```csharp
if (item.NearCastle && item.RoadDistance == RoadDistance.Far)
    issues.Add("NearCastle is incompatible with RoadDistance=Far.");
```

Delete the block that checks `RoadDistance` is one of `"Close"/"Mid"/"Far"`.

**Step 1.4 — Migrate test sites.**

Run `grep -rn '"Close"\|"Mid"\|"Far"' tests/OldenEra.Generator.Tests/Settings/ZoneContent` (or wherever validator tests live) and replace string literals with `RoadDistance.Close` / `Mid` / `Far`. Tests that asserted the "non-canonical string is rejected" path are now redundant — delete those cases (the type system enforces it).

**Step 1.5 — Build + test.**

```bash
dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

Expected: clean build, all tests pass.

**Step 1.6 — Commit.**

```bash
git add -A && git commit -m "refactor(library): retype ZoneContentItem.RoadDistance to enum"
```

---

## Task 2: DTO — rename `ContentPreset(s)` → `ZoneContentPreset(s)`

**Files:**
- Rename: `src/OldenEra.Generator/Services/ZoneContent/ContentPresets.cs` → `ZoneContentPresets.cs`
- Modify: any references (`grep -rn "ContentPreset" src tests`)

**Step 2.1 — Rename file via `git mv`.**

```bash
git mv src/OldenEra.Generator/Services/ZoneContent/ContentPresets.cs \
       src/OldenEra.Generator/Services/ZoneContent/ZoneContentPresets.cs
```

**Step 2.2 — Rename the types in the file.**

Inside the file: `ContentPreset` → `ZoneContentPreset`, `ContentPresets` → `ZoneContentPresets`. Update XML doc comments to match.

**Step 2.3 — Sweep references.**

```bash
grep -rn "ContentPreset" src tests
```

Update each call site. Likely small set: validator tests, host project (Web/WPF) call into `ContentPresets.All()`. Hosts are out of scope for Round 2 logic, but if they reference the renamed type they need updating to compile.

**Step 2.4 — Build + test.**

```bash
dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

If the WPF/Web hosts reference `ContentPresets`, the library build will still pass; full solution build won't run on Mac. That's expected and noted in the design doc. Document the cross-cutting rename in the commit so a Windows build later catches host-side fallout.

**Step 2.5 — Commit.**

```bash
git commit -m "refactor(library): rename ContentPreset(s) to ZoneContentPreset(s)"
```

---

## Task 3: DTO — add `Handle` to `ZoneContentItem`

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/ZoneContentItem.cs`
- Test: `tests/OldenEra.Generator.Tests/.../ZoneContentItemTests.cs` (existing) or add a focused test

**Step 3.1 — Failing test for the field default.**

Add a test asserting `new ZoneContentItem().Handle == null`. (Cheap, but pins the decision and gives us a concrete TDD anchor.)

**Step 3.2 — Run test to verify it fails.**

`dotnet test --filter Handle` — expected: compile error (no field).

**Step 3.3 — Add the field.**

```csharp
public string? Handle { get; set; }
```

**Step 3.4 — Run test to verify it passes.**

**Step 3.5 — Commit.**

```bash
git commit -m "feat(library): add optional Handle to ZoneContentItem"
```

---

## Task 4: DTO — replace `ContentConnectionRule` / `ContentRuleType`

**Files:**
- Delete: `src/OldenEra.Generator/Models/Generator/ContentConnectionRule.cs`
- Create: `src/OldenEra.Generator/Models/Generator/ZoneRoadDecoration.cs`
- Create: `src/OldenEra.Generator/Models/Generator/ZoneRoadEndpoint.cs`
- Modify: `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs`
- Test: `tests/OldenEra.Generator.Tests/.../ZoneRoadDecorationTests.cs` (new, minimal)

**Step 4.1 — Failing test.**

```csharp
[Fact]
public void ZoneRoadDecoration_Defaults_AreSchemaAligned()
{
    var d = new ZoneRoadDecoration();
    Assert.Equal("", d.Zone);
    Assert.Equal("Stone", d.RoadType);
    Assert.Equal(ZoneRoadEndpointKind.Connection, d.From.Kind);
    Assert.Equal("", d.From.Arg);
}
```

**Step 4.2 — Create the new types.**

`ZoneRoadEndpoint.cs`:

```csharp
namespace OldenEra.Generator.Models
{
    public enum ZoneRoadEndpointKind
    {
        Connection,
        MainObject,
        MandatoryContent,
    }

    public sealed class ZoneRoadEndpoint
    {
        public ZoneRoadEndpointKind Kind { get; set; } = ZoneRoadEndpointKind.Connection;
        public string Arg { get; set; } = "";
    }
}
```

`ZoneRoadDecoration.cs`:

```csharp
namespace OldenEra.Generator.Models
{
    public sealed class ZoneRoadDecoration
    {
        public string Zone { get; set; } = "";
        public string RoadType { get; set; } = "Stone";
        public ZoneRoadEndpoint From { get; set; } = new();
        public ZoneRoadEndpoint To { get; set; } = new();
    }
}
```

**Step 4.3 — Delete the retired file.**

```bash
git rm src/OldenEra.Generator/Models/Generator/ContentConnectionRule.cs
```

**Step 4.4 — Migrate `GeneratorSettings`.**

Rename field and retype:

```csharp
public List<ZoneRoadDecoration> ZoneRoadDecorations { get; set; } = new();
```

Replaces `public List<ContentConnectionRule> ContentConnectionRules { get; set; } = new();` (line 242 today).

**Step 4.5 — Sweep references.**

```bash
grep -rn "ContentConnectionRule\|ContentRuleType\|ContentConnectionRules" src tests
```

Each hit — likely settings serialization (`SettingsMapper`?), a test fixture or two, possibly the host shell — should either be updated or, if the reference is host-side and only settings-serialization, deleted with a TODO note pointing at Round 3 (settings persistence is explicitly Round 3 scope). Library callers must compile.

**Step 4.6 — Run test to verify it passes.**

```bash
dotnet test --filter ZoneRoadDecoration
```

**Step 4.7 — Build + full test.**

```bash
dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

**Step 4.8 — Commit.**

```bash
git commit -m "refactor(library): replace ContentConnectionRule with ZoneRoadDecoration"
```

---

## Task 5: `EmitWarning` record + code constants

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/EmitWarning.cs`
- Test: `tests/OldenEra.Generator.Tests/.../EmitWarningTests.cs` (minimal — pin the record shape)

**Step 5.1 — Failing test.**

```csharp
[Fact]
public void EmitWarning_Code_BiomeFilter_IsKnownConstant()
{
    Assert.Equal("BiomeFilter.Ignored", EmitWarning.Codes.BiomeFilterIgnored);
}
```

**Step 5.2 — Implement.**

```csharp
namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record EmitWarning(
        string Code,
        string Message,
        string? ZoneName,
        string? Sid)
    {
        public static class Codes
        {
            public const string BiomeFilterIgnored = "BiomeFilter.Ignored";
            public const string FactionAffinityIgnored = "FactionAffinity.Ignored";
            public const string PoolNonMandatoryDropped = "Pool.NonMandatoryDropped";
            public const string MinCountRangeNarrowedToMax = "MinCount.RangeNarrowedToMax";
        }
    }
}
```

**Step 5.3 — Test passes. Build + test.**

**Step 5.4 — Commit.**

```bash
git commit -m "feat(library): add EmitWarning record and code constants"
```

---

## Task 6: `ZoneContentEmitWarnings.Inspect`

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentEmitWarnings.cs`
- Test: `tests/OldenEra.Generator.Tests/.../ZoneContentEmitWarningsTests.cs`

**Step 6.1 — Write failing tests (one per code, plus one for the clean case).**

Five cases:

```csharp
[Fact]
public void Inspect_BiomeFilterPopulated_EmitsBiomeFilterIgnored() { ... }

[Fact]
public void Inspect_FactionAffinityPopulated_EmitsFactionAffinityIgnored() { ... }

[Fact]
public void Inspect_PoolGuarded_EmitsPoolNonMandatoryDropped() { ... }
// (and Unguarded, Resources — parameterise via [Theory])

[Fact]
public void Inspect_MinCountLessThanMaxCount_EmitsMinCountRangeNarrowedToMax() { ... }

[Fact]
public void Inspect_CleanItem_EmitsNoWarnings() { ... }
```

Each asserts the code, the `ZoneName` propagation, and the `Sid` propagation.

**Step 6.2 — Run, verify they fail (compile error).**

**Step 6.3 — Implement.**

```csharp
public static class ZoneContentEmitWarnings
{
    public static IReadOnlyList<EmitWarning> Inspect(ZoneContentItem item, string? zoneName)
    {
        var warnings = new List<EmitWarning>();

        if (item.BiomeFilter.Count > 0)
            warnings.Add(new EmitWarning(
                EmitWarning.Codes.BiomeFilterIgnored,
                "BiomeFilter has no schema slot and will be ignored.",
                zoneName, item.Sid));

        if (item.FactionAffinity.Count > 0)
            warnings.Add(new EmitWarning(
                EmitWarning.Codes.FactionAffinityIgnored,
                "FactionAffinity has no schema slot and will be ignored.",
                zoneName, item.Sid));

        if (item.Pool != ZoneContentPool.Mandatory)
            warnings.Add(new EmitWarning(
                EmitWarning.Codes.PoolNonMandatoryDropped,
                $"Pool '{item.Pool}' is not emittable in v1; item will be skipped.",
                zoneName, item.Sid));

        if (item.MinCount != item.MaxCount)
            warnings.Add(new EmitWarning(
                EmitWarning.Codes.MinCountRangeNarrowedToMax,
                $"Count range {item.MinCount}-{item.MaxCount} narrowed to {item.MaxCount}.",
                zoneName, item.Sid));

        return warnings;
    }
}
```

**Step 6.4 — Test passes. Build + test.**

**Step 6.5 — Commit.**

```bash
git commit -m "feat(library): add ZoneContentEmitWarnings inspector"
```

---

## Task 7: Validator gains `InspectEmit`

**Files:**
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentItemValidator.cs`
- Test: existing validator test file

**Step 7.1 — Failing test.**

```csharp
[Fact]
public void InspectEmit_DelegatesToZoneContentEmitWarnings()
{
    var item = new ZoneContentItem { Sid = "x", BiomeFilter = { "Snow" } };
    var warnings = ZoneContentItemValidator.InspectEmit(item, "side_red");
    Assert.Contains(warnings, w => w.Code == EmitWarning.Codes.BiomeFilterIgnored);
    Assert.Equal("side_red", warnings[0].ZoneName);
}
```

**Step 7.2 — Verify failure.**

**Step 7.3 — Implement.**

```csharp
public static IReadOnlyList<EmitWarning> InspectEmit(ZoneContentItem item, string? zoneName)
    => ZoneContentEmitWarnings.Inspect(item, zoneName);
```

(Single delegating method on the validator class. Existing self-consistency `Validate` stays unchanged.)

**Step 7.4 — Test passes. Build + test.**

**Step 7.5 — Commit.**

```bash
git commit -m "feat(library): expose InspectEmit on ZoneContentItemValidator"
```

---

## Task 8: `ZoneRoadDecorationEmitter`

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ZoneRoadDecorationEmitter.cs`
- Test: `tests/OldenEra.Generator.Tests/.../ZoneRoadDecorationEmitterTests.cs`

**Step 8.1 — Failing tests.**

```csharp
[Theory]
[InlineData(ZoneRoadEndpointKind.Connection, "Spawn-A-Red", "Connection")]
[InlineData(ZoneRoadEndpointKind.MainObject, "0", "MainObject")]
[InlineData(ZoneRoadEndpointKind.MandatoryContent, "name_x", "MandatoryContent")]
public void ApplyToZone_EmitsRoadWithSchemaTypeAndArgs(
    ZoneRoadEndpointKind kind, string arg, string expectedSchemaType) { ... }

[Fact]
public void ApplyToZone_HonoursRoadType_StoneAndDirt() { ... }

[Fact]
public void ReferencedItems_FindsMandatoryContentEndpoints_IgnoresOthers() { ... }
```

**Step 8.2 — Implement.**

```csharp
public static class ZoneRoadDecorationEmitter
{
    public static void ApplyToZone(
        Zone zone,
        IReadOnlyList<ZoneRoadDecoration> decorationsForThisZone)
    {
        zone.Roads ??= new List<Road>();
        foreach (var d in decorationsForThisZone)
        {
            zone.Roads.Add(new Road
            {
                Type = d.RoadType,
                From = ToSchemaEndpoint(d.From),
                To = ToSchemaEndpoint(d.To),
            });
        }
    }

    public static IReadOnlySet<string> ReferencedItems(
        IReadOnlyList<ZoneRoadDecoration> decorations)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in decorations)
        {
            if (d.From.Kind == ZoneRoadEndpointKind.MandatoryContent) set.Add(d.From.Arg);
            if (d.To.Kind   == ZoneRoadEndpointKind.MandatoryContent) set.Add(d.To.Arg);
        }
        return set;
    }

    private static Models.Unfrozen.RoadEndpoint ToSchemaEndpoint(ZoneRoadEndpoint e)
        => new() { Type = e.Kind.ToString(), Args = new List<string> { e.Arg } };
}
```

> **Note:** the simpler `ReferencedItems` returns a flat `HashSet<string>` of referenced arg names (not the `(zone, sid, index)` triple from the design). The triple was overcomplicated — what the content emitter actually needs is *"is this item's handle/auto-name referenced by any decoration?"* which is a name lookup, not a positional one. Reflect this back in the design doc as a refinement before commit (or, if discovered during impl, mention in the commit message).

**Step 8.3 — Test passes. Build + test.**

**Step 8.4 — Commit.**

```bash
git commit -m "feat(library): add ZoneRoadDecorationEmitter"
```

---

## Task 9: `ZoneContentEmitter`

**Files:**
- Create: `src/OldenEra.Generator/Services/ZoneContent/ZoneContentEmitter.cs`
- Test: `tests/OldenEra.Generator.Tests/.../ZoneContentEmitterTests.cs`

**Step 9.1 — Failing tests (seven cases per the design doc).**

```csharp
// 1. Sid-only → { sid }
// 2. IsGroup → { includeLists: [Sid] }
// 3. MaxCount=3 → row repeated 3 times
// 4. Handle="x" → name = "x"
// 5. Referenced-but-no-handle → name = "name_user_<zone>_<sid>_<index>"
// 6. NearCastle + RoadDistance.Mid → both placement rules with expected ranges
// 7. Pool=Guarded → no row emitted, warning surfaced
```

Each test sets up a `MandatoryContentGroup` with empty `Content`, calls `ZoneContentEmitter.ApplyToMandatoryGroup(group, [item], "side_red", referenced)`, then asserts on `group.Content` and the returned `EmitResult.Warnings`.

**Step 9.2 — Implement.**

```csharp
public static class ZoneContentEmitter
{
    public sealed record EmitResult(IReadOnlyList<EmitWarning> Warnings);

    public static EmitResult ApplyToMandatoryGroup(
        Models.Unfrozen.MandatoryContentGroup group,
        IReadOnlyList<ZoneContentItem> items,
        string zoneName,
        IReadOnlySet<string> referencedNames)
    {
        var warnings = new List<EmitWarning>();
        group.Content ??= new List<Models.Unfrozen.ContentItem>();

        var occurrenceBySid = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var itemWarnings = ZoneContentEmitWarnings.Inspect(item, zoneName);
            warnings.AddRange(itemWarnings);

            if (itemWarnings.Any(w => w.Code == EmitWarning.Codes.PoolNonMandatoryDropped))
                continue;

            var occurrence = occurrenceBySid.TryGetValue(item.Sid, out var n) ? n : 0;
            occurrenceBySid[item.Sid] = occurrence + 1;

            string? name = ResolveName(item, zoneName, occurrence, referencedNames);
            var rules = BuildPlacementRules(item);

            for (int copy = 0; copy < item.MaxCount; copy++)
            {
                var row = new Models.Unfrozen.ContentItem
                {
                    Name = name,
                    IsGuarded = item.IsGuarded ? true : null,
                };
                if (item.IsGroup) row.IncludeLists = new List<string> { item.Sid };
                else row.Sid = item.Sid;
                if (rules.Count > 0) row.Rules = rules;
                group.Content.Add(row);
            }
        }

        return new EmitResult(warnings);
    }

    private static string? ResolveName(
        ZoneContentItem item, string zoneName, int occurrence, IReadOnlySet<string> referencedNames)
    {
        if (!string.IsNullOrEmpty(item.Handle)) return item.Handle;
        var auto = $"name_user_{zoneName}_{item.Sid}_{occurrence}";
        return referencedNames.Contains(auto) ? auto : null;
    }

    private static List<Models.Unfrozen.ContentPlacementRule> BuildPlacementRules(ZoneContentItem item)
    {
        var rules = new List<Models.Unfrozen.ContentPlacementRule>();

        if (item.NearCastle)
            rules.Add(new() { Type = "MainObject", Args = new() { "0" },
                              TargetMin = 0.05, TargetMax = 0.25, Weight = 1 });

        if (item.RoadDistance is { } rd)
        {
            var (min, max) = rd switch
            {
                RoadDistance.Close => (0.10, 0.20),
                RoadDistance.Mid   => (0.30, 0.50),
                RoadDistance.Far   => (0.60, 0.85),
                _ => (0.0, 0.0),
            };
            rules.Add(new() { Type = "Road", Args = new(),
                              TargetMin = min, TargetMax = max, Weight = 1 });
        }

        return rules;
    }
}
```

**Step 9.3 — Tests pass. Build + test.**

**Step 9.4 — Commit.**

```bash
git commit -m "feat(library): add ZoneContentEmitter for the Mandatory pool"
```

---

## Task 10: Wire content emitter into the central mandatory builder

**Files:**
- Modify: `src/OldenEra.Generator/Services/TemplateGenerator.cs` (around lines 2818-2853)

**Step 10.1 — Identify the exact insertion sites.**

`BuildSpawnMandatoryContent(letter, ...)` builds `mandatory_content_side_<letter>` and assigns `Content = BuildPlayerZoneMandatoryContent(...)`. After that assignment, append user items.

`BuildNeutralMandatoryContent(letter, ...)` builds `mandatory_content_neutral_<letter>` similarly.

Both methods need access to `GeneratorSettings`. `BuildSpawnMandatoryContent` already takes settings-derived args; pass the full `GeneratorSettings settings` and the `IReadOnlySet<string> referencedNames` through.

**Step 10.2 — Failing integration test.**

`tests/.../TemplateGenerator.ZoneContentIntegration.cs` (or extend an existing TemplateGenerator test):

```csharp
[Fact]
public void BuildAllMandatoryContent_AppendsUserItems_ToCorrectPerZoneGroup()
{
    var settings = MakeMinimalSettings();
    settings.PlayerZoneContent.Items.Add(new ZoneContentItem
    {
        Sid = "mana_well", Handle = "user_well", Pool = ZoneContentPool.Mandatory
    });
    settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration
    {
        Zone = "side_red", RoadType = "Stone",
        From = new() { Kind = ZoneRoadEndpointKind.Connection, Arg = "Spawn-A-Red-A" },
        To   = new() { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "user_well" },
    });

    var template = new TemplateGenerator().Generate(settings);

    var sideRed = template.MandatoryContent!.Single(g => g.Name == "mandatory_content_side_red");
    Assert.Contains(sideRed.Content!, c => c.Name == "user_well" && c.Sid == "mana_well");
}
```

(Adjust `MakeMinimalSettings()` to whatever the existing test helpers offer; if there isn't one, copy the pattern from existing TemplateGenerator tests.)

**Step 10.3 — Run, verify it fails.**

**Step 10.4 — Wire the emitter.**

In `BuildAllMandatoryContent` (line 2818):

```csharp
var referencedNames = ZoneRoadDecorationEmitter.ReferencedItems(settings.ZoneRoadDecorations);
var groups = new List<MandatoryContentGroup>();

foreach (var letter in playerLetters)
{
    var group = BuildSpawnMandatoryContent(letter, settings.ZoneCfg.PlayerZoneCastles, settings.SpawnRemoteFootholds);
    var userItems = ZoneContentResolver.ResolveForZone(/* spawn scope, letter, settings */);
    if (userItems.Count > 0)
        ZoneContentEmitter.ApplyToMandatoryGroup(group, userItems, $"side_{letter}", referencedNames);
    groups.Add(group);
}

foreach (var neutralZone in neutralZones)
{
    var group = BuildNeutralMandatoryContent(neutralZone.Letter, neutralZone.CastleCount, settings.SpawnRemoteFootholds, neutralZone.Quality);
    var userItems = ZoneContentResolver.ResolveForZone(/* neutral scope, letter, tier, settings */);
    if (userItems.Count > 0)
        ZoneContentEmitter.ApplyToMandatoryGroup(group, userItems, $"neutral_{neutralZone.Letter}", referencedNames);
    groups.Add(group);
}
```

> **Note for executor:** `ZoneContentResolver`'s actual API needs a peek
> before this lands. Round 1's resolver merges Global → Tier with same-Sid
> replacement (commit `5f0bfc5`); the right call signature is whatever it
> already exposes. If the resolver's API doesn't expose a per-(scope,letter)
> accessor, add a thin wrapper rather than reshaping the resolver.

**Step 10.5 — Test passes. Build + full test suite.**

**Step 10.6 — Commit.**

```bash
git commit -m "feat(library): wire ZoneContentEmitter into BuildAllMandatoryContent"
```

---

## Task 11: Wire road-decoration emitter into the zone builders

**Files:**
- Modify: `src/OldenEra.Generator/Services/TemplateGenerator.cs` (`BuildSpawnZone`, `BuildNeutralZone`)

**Step 11.1 — Failing integration test.**

Extend the integration test from Task 10 to also assert the `Roads[]` entry on the right zone:

```csharp
var redZone = template.Zones!.Single(z => z.Name == "side_red");
Assert.Contains(redZone.Roads!, r =>
    r.Type == "Stone" &&
    r.From!.Type == "Connection" && r.From.Args![0] == "Spawn-A-Red-A" &&
    r.To!.Type == "MandatoryContent" && r.To.Args![0] == "user_well");
```

**Step 11.2 — Run, verify it fails.**

**Step 11.3 — Wire the emitter.**

In `BuildSpawnZone` and `BuildNeutralZone`, after the existing `Roads` population:

```csharp
var userDecorations = settings.ZoneRoadDecorations.Where(d => d.Zone == zone.Name).ToList();
if (userDecorations.Count > 0)
    ZoneRoadDecorationEmitter.ApplyToZone(zone, userDecorations);
```

Both methods need access to `settings`. They likely already do (via the central call); if not, thread it through. Check the existing signatures and decide whether to pass `settings` or just `IReadOnlyList<ZoneRoadDecoration>`.

**Step 11.4 — Test passes. Full build + test.**

**Step 11.5 — Commit.**

```bash
git commit -m "feat(library): wire ZoneRoadDecorationEmitter into zone builders"
```

---

## Task 12: No-op guard test

**Files:**
- Test: `tests/OldenEra.Generator.Tests/.../TemplateGenerator.ZoneContentNoOp.cs`

**Step 12.1 — Test.**

```csharp
[Fact]
public void EmptyUserInputs_ProduceUnchangedMandatoryGroups()
{
    var settings = MakeMinimalSettings();
    Assert.Empty(settings.PlayerZoneContent.Items);
    Assert.Empty(settings.NeutralZoneContent.Tiers);   // or whatever the field is
    Assert.Empty(settings.ZoneRoadDecorations);

    var template = new TemplateGenerator().Generate(settings);

    foreach (var group in template.MandatoryContent!)
    {
        // Every entry must be a curated-content row — no auto-generated user names.
        Assert.DoesNotContain(group.Content!, c => c.Name?.StartsWith("name_user_") == true);
    }

    foreach (var zone in template.Zones!)
    {
        // Every road must come from the existing road-generation pipeline; user
        // decorations are absent. The cleanest check is that no Road has
        // `Type == "Stone"` AND a MandatoryContent endpoint with `name_user_*`,
        // but easier: count of roads in a snapshot vs. count of roads now.
        // Use whichever invariant is cheapest to express against the existing
        // road generator's output for an empty-content config.
    }
}
```

(The structural guard from the design — "emitter not invoked when inputs empty" — is enforced by the `if (userItems.Count > 0)` and `if (userDecorations.Count > 0)` early-returns at the wiring sites. This test is the externally-observable consequence: empty inputs leave output free of any user signature.)

**Step 12.2 — Test passes immediately (the wiring already short-circuits). Build + test.**

**Step 12.3 — Commit.**

```bash
git commit -m "test(library): pin no-op guarantee for empty zone-content inputs"
```

---

## Task 13: Final pass — design doc note + plan parity

**Step 13.1 — Reflect any deviations.**

If `ReferencedItems`'s flat `HashSet<string>` (Task 8) ended up replacing the design's `(zone, sid, index)` triple, write a one-paragraph note at the bottom of `docs/plans/2026-05-12-zone-content-round-2-design.md` explaining the simplification.

If `ZoneContentResolver` needed a thin wrapper to expose per-(scope,letter) lookup (Task 10), note that too.

**Step 13.2 — Final build + full test.**

```bash
dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj
dotnet test  tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

Expected: zero failures.

**Step 13.3 — Commit any doc changes.**

```bash
git commit -m "docs(plans): reflect Round 2 implementation deviations"
```

---

## Out of scope (do not implement in this plan)

- `.oetgs` settings persistence. If a `SettingsMapper` reference breaks on `ContentConnectionRule` removal, gate the change with a TODO note pointing at Round 3.
- Web UI / WPF host UI. If the rename in Task 2 breaks host code, the library still compiles; full solution build doesn't run on Mac so host fallout will surface on the next Windows CI pass.
- Share-codec changes / fixture migrations.
- Round 1 resolver internals. Treat the resolver's behaviour as fixed; the wiring task adds at most a thin accessor.

## When to stop and ask

- If `ZoneContentResolver`'s API is materially different from "give me the items for this (scope, letter)" and a thin wrapper isn't enough.
- If `BuildSpawnZone`/`BuildNeutralZone` don't actually have access to `GeneratorSettings`. Threading settings through is fine; reshaping the call graph is not.
- If a test reveals that mandatory groups carry per-zone state we haven't accounted for (e.g. quality tier affecting names).
- If host projects fail to compile in a way that blocks library tests from running.
