# Zone Content Round 3 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Persist `PlayerZoneContent`, `NeutralZoneContent`, and `ZoneRoadDecorations` through `.oetgs` and the share-codec, enum-ify `RoadType`, and decouple emitter schema strings from enum identifiers.

**Architecture:** Library-only changes. New enum (`ZoneRoadType`). Three new fields on `SettingsFile` mirroring the `GeneratorSettings` shape. Mapper round-trip. Global `JsonStringEnumConverter` registered on every options block touching `SettingsFile` so enums persist as strings. Six commits, each independently bisectable.

**Tech Stack:** C# / .NET 8, `System.Text.Json`, xUnit, Shouldly. Mac-friendly: build via `OldenEra.Generator.csproj`, test via `OldenEra.Generator.Tests.csproj`.

**Companion design doc:** `docs/plans/2026-05-13-zone-content-round-3-design.md`

**Worktree:** `.worktrees/zone-content-round-3`, branch `feature/zone-content-round-3`, off `main`.

**Build / test commands** (run inside the worktree):
- `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj`
- `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`

The `OldenEra.slnx` solution build is not used on Mac (WPF host doesn't build there).

---

## Task 1: Enum-ify `ZoneRoadDecoration.RoadType`

**Why first:** persistence shouldn't ship a string field for something we're about to make an enum. Doing it before `SettingsFile` references the type means there's never a "string roadType in .oetgs" wire form.

**Files:**
- Create: `src/OldenEra.Generator/Models/Generator/ZoneRoadType.cs`
- Modify: `src/OldenEra.Generator/Models/Generator/ZoneRoadDecoration.cs`
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneRoadDecorationEmitter.cs`
- Modify: any test file that constructs `ZoneRoadDecoration` with a string `RoadType`. Find with: `grep -rn 'RoadType\s*=\s*"' tests --include='*.cs'`

**Step 1: Write the failing test**

In `tests/OldenEra.Generator.Tests/Settings/ZoneContent/ZoneRoadTypeTests.cs` (create if absent):

```csharp
using OldenEra.Generator.Models;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests.Settings.ZoneContent;

public class ZoneRoadTypeTests
{
    [Fact]
    public void Default_RoadType_is_Stone()
    {
        var d = new ZoneRoadDecoration();
        d.RoadType.ShouldBe(ZoneRoadType.Stone);
    }
}
```

**Step 2: Run, expect compile failure**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~ZoneRoadTypeTests"` — fails to compile because `ZoneRoadType` doesn't exist.

**Step 3: Add the enum and retype the field**

Create `ZoneRoadType.cs`:

```csharp
namespace OldenEra.Generator.Models
{
    public enum ZoneRoadType { Stone, Dirt }
}
```

Edit `ZoneRoadDecoration.cs`:

```csharp
namespace OldenEra.Generator.Models
{
    public sealed class ZoneRoadDecoration
    {
        public string Zone { get; set; } = "";
        public ZoneRoadType RoadType { get; set; } = ZoneRoadType.Stone;
        public ZoneRoadEndpoint From { get; set; } = new();
        public ZoneRoadEndpoint To { get; set; } = new();
    }
}
```

**Step 4: Update the emitter to handle the new type**

Edit `ZoneRoadDecorationEmitter.cs`. Where it currently sets `Type = d.RoadType` (string-to-string assignment that no longer compiles), call `d.RoadType.ToString()` *temporarily* — Task 2 replaces this with the explicit switch.

```csharp
zone.Roads.Add(new SchemaRoad
{
    Type = d.RoadType.ToString(),  // temporary; Task 2 replaces with RoadTypeToSchemaType
    From = ToSchemaEndpoint(d.From),
    To   = ToSchemaEndpoint(d.To),
});
```

**Step 5: Migrate any existing tests that hard-code a string RoadType**

Run: `grep -rn 'RoadType\s*=\s*"' tests --include='*.cs'`

For each match, replace `RoadType = "Stone"` with `RoadType = ZoneRoadType.Stone` (similarly `"Dirt"` → `ZoneRoadType.Dirt`). Do not migrate matches that refer to other types' `RoadType` (`BordersRoadsSettings.RoadType`, `SettingsFile.RoadType`, `TierOverrideFile`-adjacent — those are unrelated string fields).

**Step 6: Build and run all generator tests**

```
dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj
dotnet test  tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

All green. The new ZoneRoadTypeTests test passes (default is Stone).

**Step 7: Commit**

```
git add -A
git commit -m "feat(library): enum-ify ZoneRoadDecoration.RoadType

Replaces the string field with ZoneRoadType { Stone, Dirt }. Round 3
persistence and share-codec ship the wire form as an enum from day one.
Emitter still calls .ToString() temporarily; Task 2 replaces with an
explicit schema-string switch."
```

---

## Task 2: Decouple emitter schema strings from enum identifiers

**Why:** the Round 2 reviewer's flag. Schema strings should never be derived from `enum.ToString()`; one rename of an enum value would silently change the wire form.

**Files:**
- Modify: `src/OldenEra.Generator/Services/ZoneContent/ZoneRoadDecorationEmitter.cs`
- Create: `tests/OldenEra.Generator.Tests/Settings/ZoneContent/ZoneRoadDecorationEmitterSchemaMappingTests.cs`

**Step 1: Write the failing test**

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services.ZoneContent;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests.Settings.ZoneContent;

public class ZoneRoadDecorationEmitterSchemaMappingTests
{
    [Theory]
    [InlineData(ZoneRoadType.Stone, "Stone")]
    [InlineData(ZoneRoadType.Dirt,  "Dirt")]
    public void RoadType_emits_explicit_schema_string(ZoneRoadType type, string expected)
    {
        var zone = new Zone();
        var dec  = new ZoneRoadDecoration
        {
            RoadType = type,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "x" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "y" },
        };
        ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { dec });
        zone.Roads!.Single().Type.ShouldBe(expected);
    }

    [Theory]
    [InlineData(ZoneRoadEndpointKind.Connection,       "Connection")]
    [InlineData(ZoneRoadEndpointKind.MainObject,       "MainObject")]
    [InlineData(ZoneRoadEndpointKind.MandatoryContent, "MandatoryContent")]
    public void EndpointKind_emits_explicit_schema_string(ZoneRoadEndpointKind kind, string expected)
    {
        var zone = new Zone();
        var dec  = new ZoneRoadDecoration
        {
            RoadType = ZoneRoadType.Stone,
            From = new ZoneRoadEndpoint { Kind = kind, Arg = "a" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection, Arg = "b" },
        };
        ZoneRoadDecorationEmitter.ApplyToZone(zone, new[] { dec });
        zone.Roads!.Single().From!.Type.ShouldBe(expected);
    }
}
```

**Step 2: Run, expect pass**

The temporary `ToString()` happens to produce identical strings, so the tests pass *today*. That's exactly the coupling we're closing — the test pins the expected output, then we change the implementation to explicit switches without changing behaviour.

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SchemaMappingTests"` — green.

**Step 3: Replace `ToString()` with explicit switches**

Edit `ZoneRoadDecorationEmitter.cs`:

```csharp
public static void ApplyToZone(
    Zone zone,
    IReadOnlyList<ZoneRoadDecoration> decorationsForThisZone)
{
    zone.Roads ??= new List<SchemaRoad>();
    foreach (var d in decorationsForThisZone)
    {
        zone.Roads.Add(new SchemaRoad
        {
            Type = RoadTypeToSchemaType(d.RoadType),
            From = ToSchemaEndpoint(d.From),
            To   = ToSchemaEndpoint(d.To),
        });
    }
}

private static SchemaRoadEndpoint ToSchemaEndpoint(ZoneRoadEndpoint e)
    => new() { Type = KindToSchemaType(e.Kind), Args = new List<string> { e.Arg } };

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

**Step 4: Run all tests**

```
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

All green.

**Step 5: Commit**

```
git add -A
git commit -m "refactor(library): explicit schema-string mapping in ZoneRoadDecorationEmitter

Replaces enum.ToString() with KindToSchemaType / RoadTypeToSchemaType
switches. Future enum renames now require an explicit schema-string
update; no silent wire-form drift."
```

---

## Task 3: Pin behaviour for unmatched MandatoryContent endpoint arg

**Why:** Round 2 review follow-up. Today, a road decoration whose `MandatoryContent` arg matches no item `Handle` and isn't a known auto-name still produces a road in the schema — it just doesn't trigger any name-tagging on a content row. We pin that behaviour with a regression test.

**Files:**
- Create: `tests/OldenEra.Generator.Tests/Settings/ZoneContent/ZoneRoadDecorationUnmatchedHandleTests.cs`

**Step 1: Find a minimal generator entry point that exercises both emitters**

`grep -n "Generate(.*GeneratorSettings" src/OldenEra.Generator/Services/TemplateGenerator.cs | head` — `TemplateGenerator.Generate(settings)` returns the full `RmgTemplate`. Existing tests in `tests/OldenEra.Generator.Tests/` already use this; copy the smallest existing pattern (look for a test that builds a `GeneratorSettings`, calls `TemplateGenerator.Generate`, and asserts on the output).

**Step 2: Write the test**

```csharp
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests.Settings.ZoneContent;

public class ZoneRoadDecorationUnmatchedHandleTests
{
    [Fact]
    public void Decoration_with_unmatched_MandatoryContent_arg_still_emits_road_and_no_row_gains_name()
    {
        var settings = new GeneratorSettings
        {
            // Use the same minimal-config builder existing tests use.
            // (Adjust to match the smallest pattern in the test suite — usually
            // a 2-player default with TemplateName set.)
            PlayerCount = 2,
            ZoneRoadDecorations =
            {
                new ZoneRoadDecoration
                {
                    // Use an actual zone name. Existing generator names spawn zones
                    // "side_<letter>"; pick the first ("side_a") since playerCount >= 1.
                    Zone = "side_a",
                    RoadType = ZoneRoadType.Stone,
                    From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "ghost_handle_does_not_exist" },
                    To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" }
                }
            }
        };

        var template = TemplateGenerator.Generate(settings);

        // 1. Road decoration is preserved on the right zone.
        var zone = template.Variants!.SelectMany(v => v.Zones ?? new()).First(z => z.Name == "side_a");
        zone.Roads.ShouldNotBeNull();
        zone.Roads!.Any(r => r.From?.Args?.FirstOrDefault() == "ghost_handle_does_not_exist").ShouldBeTrue();

        // 2. No mandatory-content row gained a Name matching the ghost arg.
        var allMandatoryRows = template.MandatoryContent ?? new();
        allMandatoryRows
            .SelectMany(g => g.Content ?? new())
            .Any(row => row.Name == "ghost_handle_does_not_exist")
            .ShouldBeFalse();
    }
}
```

> **Calibration note:** the exact zone name (`side_a`) and connection arg
> format depend on what the generator emits today. Before committing,
> run an existing generator integration test in debug-print mode (or
> `JsonSerializer.Serialize` the template) to read the actual zone name
> and a real connection arg out of a vanilla 2-player generation. Adjust
> the strings to match — *do not* invent them. The Connection arg must be
> shaped like real connections so this stays a regression test for the
> MandatoryContent miss specifically, not a string-shape mismatch.

**Step 3: Run, expect pass**

`dotnet test ... --filter "FullyQualifiedName~UnmatchedHandle"` — passes.

**Step 4: Commit**

```
git commit -am "test(library): pin behaviour for unmatched MandatoryContent decoration arg

Regression test: a road decoration whose MandatoryContent arg matches
no item Handle and no auto-name is still emitted as a road; no content
row silently gains a Name from the ghost arg."
```

---

## Task 4: Migrate to global `JsonStringEnumConverter`

**Why:** decision 1 in the design doc. With `JsonStringEnumConverter` registered on every options block touching `SettingsFile`, every enum in the persisted form lands as a string. `JsonStringEnumConverter.AllowIntegerValues` defaults to `true`, so the three shipped `.oetgs` presets (which currently hold `"topology": <int>`) keep loading.

**Files (modify):**
- `src/OldenEra.Generator/Services/SettingsShareCodec.cs` (both `JsonOptions` and `LenientOptions`)
- `src/OldenEra.Generator/Services/PresetCatalog.cs` (`SettingsOptions`)
- `tests/OldenEra.Generator.Tests/SettingsFileSeedTests.cs` (its `Opts`)
- `tests/OldenEra.Generator.Tests/PresetCatalogTests.cs` (its `opts`)
- `tests/OldenEra.Generator.Tests/SeedDeterminismTests.cs` (its `Opts`)

> Do **not** modify `tests/OldenEra.TemplateEditor.Tests/HostParityTests.cs` —
> it serialises `RmgTemplate`, which is the schema-output path, not the
> persistence path. Out of scope.

**Step 1: Write the failing test**

In a new file `tests/OldenEra.Generator.Tests/SettingsFileEnumStringTests.cs`:

```csharp
using System.Text.Json;
using OldenEra.Generator.Models;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsFileEnumStringTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public void Topology_serialises_as_string()
    {
        var s = new SettingsFile { Topology = MapTopology.Hub };
        var json = JsonSerializer.Serialize(s, Opts);
        json.ShouldContain("\"topology\":\"Hub\"");
    }

    [Fact]
    public void Topology_still_reads_legacy_int_form()
    {
        // Existing shipped .oetgs files use ints; AllowIntegerValues defaults true.
        var json = "{\"topology\":4}";
        var s = JsonSerializer.Deserialize<SettingsFile>(json, Opts)!;
        s.Topology.ShouldBe((MapTopology)4);
    }
}
```

**Step 2: Run, expect first test to fail (writes int), second to pass**

`dotnet test ... --filter "FullyQualifiedName~SettingsFileEnumStringTests"` — first fails, second passes.

**Step 3: Register the converter on every options block touching SettingsFile**

In each of the five files listed above, find the `JsonSerializerOptions` block and add:

```csharp
Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
```

If the block already has a `Converters` collection, append rather than replace.

For `SettingsShareCodec.cs`:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = false,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
};

private static readonly JsonSerializerOptions LenientOptions = new()
{
    PropertyNameCaseInsensitive = true,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
};
```

Apply the analogous edit in `PresetCatalog.SettingsOptions` and the three test options blocks.

**Step 4: Run all tests**

```
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

All green, including the new enum-string tests.

**Step 5: Re-serialise the three shipped preset .oetgs files**

The fixtures still read fine (legacy int form), but for hygiene we re-emit them so the topology value is a string. Use a small one-shot:

```bash
dotnet run --project src/OldenEra.Generator/OldenEra.Generator.csproj -- --help 2>/dev/null || true
```

There's no built-in CLI for this. Do it via a throwaway test:

Add (and then delete after running) `tests/OldenEra.Generator.Tests/_OneShot_RewritePresets.cs`:

```csharp
using System.IO;
using System.Text.Json;
using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class _OneShot_RewritePresets
{
    [Fact(Skip = "Run manually with --filter to rewrite shipped presets to string-enum form.")]
    public void Rewrite()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        var dir = Path.Combine(SolutionRoot(), "src/OldenEra.Generator/Resources/Presets");
        foreach (var path in Directory.GetFiles(dir, "*.oetgs"))
        {
            var text = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<SettingsFile>(text, opts)!;
            File.WriteAllText(path, JsonSerializer.Serialize(s, opts));
        }
    }

    private static string SolutionRoot()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null && !File.Exists(Path.Combine(d.FullName, "OldenEra.slnx"))) d = d.Parent;
        return d!.FullName;
    }
}
```

Run it once with the `Skip` removed (or via `--filter "_OneShot_RewritePresets"` after temporarily removing the skip), confirm the three `.oetgs` files now hold `"topology": "..."`, then **delete the throwaway file**.

`git diff src/OldenEra.Generator/Resources/Presets/` should show only the topology field flipping from int to string (and possibly slight whitespace from the rewrite — that's fine).

**Step 6: Re-run all tests**

```
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
```

`PresetCatalogTests` still passes — it loads the rewritten presets. All green.

**Step 7: Commit**

```
git add -A
git commit -m "feat(library): persist Generator enums as strings in .oetgs and share string

Registers JsonStringEnumConverter on every JsonSerializerOptions block
touching SettingsFile (share-codec, preset catalog, test harness).
Re-serialises the three shipped presets so 'topology' lands as a string
('Random', 'Hub', etc.). Reads still accept the legacy int form via
AllowIntegerValues=true (the converter's default)."
```

---

## Task 5: Add zone-content fields to `SettingsFile`

**Why:** persistence. `GeneratorSettings` already exposes `PlayerZoneContent`, `NeutralZoneContent`, `ZoneRoadDecorations`; `SettingsFile` needs the same fields so the mapper has somewhere to write them.

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/SettingsFile.cs`
- Create: `tests/OldenEra.Generator.Tests/SettingsFileZoneContentRoundTripTests.cs`

**Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Text.Json;
using OldenEra.Generator.Models;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsFileZoneContentRoundTripTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static SettingsFile Fixture()
    {
        var f = new SettingsFile { TemplateName = "round3" };
        f.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well",
            Handle = "name_user_mana_a",
            IsGroup = false,
            MinCount = 1,
            MaxCount = 2,
            Pool = ZoneContentPool.Mandatory,
            IsGuarded = true,
            NearCastle = true,
            RoadDistance = RoadDistance.Mid,
            FactionAffinity = new() { "haven" },
            BiomeFilter = new() { "grass" },
        });
        f.NeutralZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "pandora_box",
            IsGroup = true,
            MaxCount = 3,
            Pool = ZoneContentPool.Mandatory,
            RoadDistance = RoadDistance.Far,
        });
        f.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "side_a",
            RoadType = ZoneRoadType.Dirt,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "name_user_mana_a" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" },
        });
        return f;
    }

    [Fact]
    public void RoundTrips_PlayerZoneContent_NeutralZoneContent_and_ZoneRoadDecorations()
    {
        var json  = JsonSerializer.Serialize(Fixture(), Opts);
        var back  = JsonSerializer.Deserialize<SettingsFile>(json, Opts)!;

        back.PlayerZoneContent.Items.Count.ShouldBe(1);
        back.PlayerZoneContent.Items[0].Handle.ShouldBe("name_user_mana_a");
        back.PlayerZoneContent.Items[0].RoadDistance.ShouldBe(RoadDistance.Mid);
        back.PlayerZoneContent.Items[0].FactionAffinity.ShouldBe(new[] { "haven" });

        back.NeutralZoneContent.Items.Count.ShouldBe(1);
        back.NeutralZoneContent.Items[0].MaxCount.ShouldBe(3);
        back.NeutralZoneContent.Items[0].RoadDistance.ShouldBe(RoadDistance.Far);

        back.ZoneRoadDecorations.Count.ShouldBe(1);
        back.ZoneRoadDecorations[0].RoadType.ShouldBe(ZoneRoadType.Dirt);
        back.ZoneRoadDecorations[0].From.Kind.ShouldBe(ZoneRoadEndpointKind.MandatoryContent);
    }

    [Fact]
    public void Enums_persist_as_strings_in_the_payload()
    {
        var json = JsonSerializer.Serialize(Fixture(), Opts);
        json.ShouldContain("\"roadDistance\":\"Mid\"");
        json.ShouldContain("\"roadType\":\"Dirt\"");
        json.ShouldContain("\"kind\":\"MandatoryContent\"");
    }
}
```

**Step 2: Run, expect compile failure**

`SettingsFile` doesn't have these fields yet.

**Step 3: Add the fields to SettingsFile.cs**

Append before the closing brace of `SettingsFile`:

```csharp
[JsonPropertyName("playerZoneContent")]
public ZoneContentList PlayerZoneContent { get; set; } = new();

[JsonPropertyName("neutralZoneContent")]
public NeutralZoneContent NeutralZoneContent { get; set; } = new();

[JsonPropertyName("zoneRoadDecorations")]
public List<ZoneRoadDecoration> ZoneRoadDecorations { get; set; } = new();
```

Confirm `ZoneContentList`, `NeutralZoneContent`, and `ZoneRoadDecoration` are accessible from `OldenEra.Generator.Models` (the `using` clause at the top of `SettingsFile.cs` already pulls in that namespace — they should resolve).

**Step 4: Run the new test**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SettingsFileZoneContentRoundTripTests"` — green.

**Step 5: Run the full suite**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` — green.

**Step 6: Commit**

```
git commit -am "feat(library): persist PlayerZoneContent, NeutralZoneContent, ZoneRoadDecorations on SettingsFile

Adds the three Round 1/2 zone-content surfaces to the .oetgs shape.
Round-trip test pins enum-as-string for RoadDistance, ZoneRoadType,
ZoneRoadEndpointKind."
```

---

## Task 6: Wire the new fields through `SettingsMapper` (with reflection guard)

**Why:** the mapper is the boundary between persistence and the generator. Without these lines, a `.oetgs` round-trip drops the new fields silently.

**Files:**
- Modify: `src/OldenEra.Generator/Services/SettingsMapper.cs`
- Create: `tests/OldenEra.Generator.Tests/SettingsMapperZoneContentRoundTripTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsMapperZoneContentRoundTripTests
{
    private static GeneratorSettings BuildPopulated()
    {
        var g = new GeneratorSettings { TemplateName = "round3" };
        g.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well", Handle = "h", IsGroup = false,
            MinCount = 1, MaxCount = 2, Pool = ZoneContentPool.Mandatory,
            IsGuarded = true, NearCastle = true,
            RoadDistance = RoadDistance.Mid,
            FactionAffinity = new() { "haven" },
            BiomeFilter = new() { "grass" },
        });
        g.NeutralZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "pandora_box", IsGroup = true, MinCount = 1, MaxCount = 3,
            Pool = ZoneContentPool.Mandatory, IsGuarded = false, NearCastle = false,
            RoadDistance = RoadDistance.Far,
            FactionAffinity = new() { "necro" },
            BiomeFilter = new() { "snow" },
        });
        g.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "side_a",
            RoadType = ZoneRoadType.Dirt,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "h" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" },
        });
        return g;
    }

    [Fact]
    public void Mapper_round_trips_zone_content_surfaces()
    {
        var original = BuildPopulated();
        var file = SettingsMapper.ToFile(original, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        back.PlayerZoneContent.Items.Count.ShouldBe(1);
        back.PlayerZoneContent.Items[0].Handle.ShouldBe("h");
        back.PlayerZoneContent.Items[0].RoadDistance.ShouldBe(RoadDistance.Mid);

        back.NeutralZoneContent.Items.Count.ShouldBe(1);
        back.NeutralZoneContent.Items[0].MaxCount.ShouldBe(3);

        back.ZoneRoadDecorations.Count.ShouldBe(1);
        back.ZoneRoadDecorations[0].RoadType.ShouldBe(ZoneRoadType.Dirt);
    }

    /// <summary>
    /// Reflection guard: every public property on the new DTO types must be
    /// non-default in BuildPopulated(). Future-added properties fail this until
    /// the fixture is updated, which forces SettingsMapper coverage forward.
    /// </summary>
    [Theory]
    [InlineData(typeof(ZoneContentItem))]
    [InlineData(typeof(ZoneRoadDecoration))]
    [InlineData(typeof(ZoneRoadEndpoint))]
    public void Fixture_populates_every_public_property(Type type)
    {
        var g = BuildPopulated();
        IEnumerable<object> instances = type switch
        {
            _ when type == typeof(ZoneContentItem) =>
                g.PlayerZoneContent.Items.Cast<object>()
                 .Concat(g.NeutralZoneContent.Items.Cast<object>()),
            _ when type == typeof(ZoneRoadDecoration) => g.ZoneRoadDecorations.Cast<object>(),
            _ when type == typeof(ZoneRoadEndpoint) =>
                g.ZoneRoadDecorations.SelectMany(d => new object[] { d.From, d.To }),
            _ => throw new ArgumentException(null, nameof(type)),
        };

        foreach (var instance in instances)
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(instance);
            var isDefault = value switch
            {
                null            => true,
                string s        => string.IsNullOrEmpty(s),
                System.Collections.ICollection c => c.Count == 0,
                bool b          => !b,                       // default(bool) is false
                int i           => i == 0,
                Enum e          => Convert.ToInt32(e) == 0,
                _               => Equals(value, Activator.CreateInstance(prop.PropertyType)),
            };
            isDefault.ShouldBeFalse(
                $"{type.Name}.{prop.Name} is at its default — populate it in BuildPopulated() so the round-trip exercises it.");
        }
    }
}
```

> **Calibration note for the reflection guard:** some properties default
> to `false` legitimately (e.g., `IsGroup` is `true` on the neutral item;
> `NearCastle` is `false` on the neutral item). The guard checks
> *across all instances* via the `foreach (var instance in instances)`
> loop — every property must be non-default in *at least one* instance.
> Adjust the loop semantics to "any instance is non-default" if needed:

```csharp
// Replace the inner foreach with:
var allInstances = instances.ToList();
foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    var anyNonDefault = allInstances.Any(inst =>
    {
        var v = prop.GetValue(inst);
        // ... same isDefault logic, return !isDefault
    });
    anyNonDefault.ShouldBeTrue(
        $"No fixture instance has a non-default {type.Name}.{prop.Name}; populate one.");
}
```

Pick the per-property "any instance" form — it's the one that actually
catches "added a field, forgot the fixture". The fixture should populate
both instances such that every property is non-default in at least one.

**Step 2: Run, expect failure**

The mapper doesn't carry the new fields yet, so the round-trip test fails (lists empty after `FromFile`).

**Step 3: Add the mapper lines**

Edit `SettingsMapper.cs` `FromFile`. Find the spot where the `GeneratorSettings` object literal closes (around line 130-something — look for `HighTier = TierFromFile(s.TierHigh),` or the final brace of the `new GeneratorSettings { ... }`). After the object initializer, add:

```csharp
settings.PlayerZoneContent    = s.PlayerZoneContent    ?? new();
settings.NeutralZoneContent   = s.NeutralZoneContent   ?? new();
settings.ZoneRoadDecorations  = s.ZoneRoadDecorations  ?? new();
```

(If those properties are reachable inside the initializer, set them there instead. Either form is fine; consistency with surrounding lines wins.)

In `ToFile`, find the matching spot and add:

```csharp
file.PlayerZoneContent    = g.PlayerZoneContent;
file.NeutralZoneContent   = g.NeutralZoneContent;
file.ZoneRoadDecorations  = g.ZoneRoadDecorations;
```

The mapper currently shares list references throughout (look at how `GlobalBans` is handled for the precedent — it deep-copies that one). For zone-content, **share references** to match `Bonuses.Resources` style; document the choice in a one-line comment if it stands out:

```csharp
// Reference-shared: matches Bonuses/Resources pattern. Round 4 UI must clone before mutating.
```

**Step 4: Run the new test**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SettingsMapperZoneContentRoundTripTests"` — green.

**Step 5: Run the full suite**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` — green.

**Step 6: Commit**

```
git commit -am "feat(library): SettingsMapper round-trips zone-content surfaces

Maps PlayerZoneContent, NeutralZoneContent, and ZoneRoadDecorations
through both halves of SettingsMapper (FromFile / ToFile). Reflection
guard test asserts every public property on the new DTO types is
non-default in at least one fixture instance, so future-added fields
can't silently drop from .oetgs round-trips."
```

---

## Task 7: Share-codec round-trip test

**Why:** decision 5. The share-codec serialises `SettingsFile` directly through `JsonOptions`, so Tasks 4 + 5 already give us the round-trip behaviour. This task pins it with a regression test.

**Files:**
- Create: `tests/OldenEra.Generator.Tests/SettingsShareCodecZoneContentTests.cs`

> Locating the test in `OldenEra.Generator.Tests` (not `OldenEra.TemplateEditor.Tests`) so the project still builds on Mac. The existing `SettingsShareCodecSeedTests.cs` lives there too — same precedent.

**Step 1: Write the test**

```csharp
using System.Collections.Generic;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Shouldly;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsShareCodecZoneContentTests
{
    private static SettingsFile Fixture()
    {
        var f = new SettingsFile { TemplateName = "round3-share" };
        f.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well", Handle = "h", IsGroup = false,
            MinCount = 1, MaxCount = 2,
            Pool = ZoneContentPool.Mandatory,
            IsGuarded = true, NearCastle = true,
            RoadDistance = RoadDistance.Mid,
            FactionAffinity = new() { "haven" }, BiomeFilter = new() { "grass" },
        });
        f.NeutralZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "pandora_box", IsGroup = true,
            MinCount = 1, MaxCount = 3,
            Pool = ZoneContentPool.Mandatory,
            RoadDistance = RoadDistance.Far,
            FactionAffinity = new() { "necro" }, BiomeFilter = new() { "snow" },
        });
        f.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "side_a",
            RoadType = ZoneRoadType.Dirt,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "h" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" },
        });
        return f;
    }

    [Fact]
    public void Encode_then_decode_preserves_zone_content_surfaces()
    {
        var original = Fixture();
        var encoded  = SettingsShareCodec.Encode(original);
        encoded.Length.ShouldBeLessThan(SettingsShareCodec.MaxEncodedLength);

        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        status.ShouldBe(SettingsShareCodec.DecodeStatus.Ok);
        decoded.ShouldNotBeNull();

        decoded!.PlayerZoneContent.Items.Count.ShouldBe(1);
        decoded.PlayerZoneContent.Items[0].Handle.ShouldBe("h");
        decoded.PlayerZoneContent.Items[0].RoadDistance.ShouldBe(RoadDistance.Mid);

        decoded.NeutralZoneContent.Items.Count.ShouldBe(1);
        decoded.NeutralZoneContent.Items[0].MaxCount.ShouldBe(3);

        decoded.ZoneRoadDecorations.Count.ShouldBe(1);
        decoded.ZoneRoadDecorations[0].RoadType.ShouldBe(ZoneRoadType.Dirt);
        decoded.ZoneRoadDecorations[0].From.Kind.ShouldBe(ZoneRoadEndpointKind.MandatoryContent);
    }

    [Fact]
    public void Empty_zone_content_encodes_decodes_clean()
    {
        var f = new SettingsFile { TemplateName = "empty" };
        var encoded = SettingsShareCodec.Encode(f);
        var back    = SettingsShareCodec.TryDecode(encoded, out _);
        back.ShouldNotBeNull();
        back!.PlayerZoneContent.Items.Count.ShouldBe(0);
        back.NeutralZoneContent.Items.Count.ShouldBe(0);
        back.ZoneRoadDecorations.Count.ShouldBe(0);
    }
}
```

**Step 2: Run, expect pass**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SettingsShareCodecZoneContentTests"` — green (no code change needed; Tasks 4 + 5 did it).

**Step 3: Run the full suite**

`dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` — green.

**Step 4: Commit**

```
git commit -am "test(library): pin SettingsShareCodec round-trip for zone-content surfaces

PlayerZoneContent + NeutralZoneContent + ZoneRoadDecorations encode and
decode through the share-codec, with enums in their string form. Empty
defaults round-trip clean."
```

---

## Final review pass

After Task 7 commits:

1. Run the full suite once more end-to-end:
   ```
   dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj
   dotnet test  tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj
   ```
2. Use `superpowers:requesting-code-review` to spawn a final reviewer pass over the whole branch diff.
3. Address any reviewer findings; if material, add a follow-up commit. If trivial polish, fix in place (still pre-MR).
4. Confirm no host-UI files were touched: `git diff main --name-only | grep -E '(WPF|Web)' || echo "clean"` (expect "clean").
5. Use `superpowers:finishing-a-development-branch` to create the PR via `gh pr create --repo rannes/Olden-Era---Template-Generator` (this fork's upstream).

## Out of scope (Round 4)

- Web UI / WPF UI for zone content.
- Master+detail panels, presets, inspect-defaults.
- Surfacing `EmitWarning` to the user.
- Experimental-gating UX changes.
