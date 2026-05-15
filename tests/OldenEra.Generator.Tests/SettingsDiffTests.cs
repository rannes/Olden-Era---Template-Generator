using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-805 — settings-vs-preset diff.
/// </summary>
public class SettingsDiffTests
{
    [Fact]
    public void Identical_Settings_Yield_Empty_Diff()
    {
        var preset = new PresetCatalog().Load("jebus-like");
        // "Fresh preset load" — current settings round-trip through the same
        // catalog Load, which is what the host does when the user just clicked
        // "Load Preset…". Diff must be empty.
        var current = new PresetCatalog().Load("jebus-like");

        var rows = SettingsDiff.Compute(preset, current);

        Assert.Empty(rows);
    }

    [Fact]
    public void Three_Field_Changes_Produce_Three_Rows()
    {
        var preset = new PresetCatalog().Load("jebus-like");
        var current = new PresetCatalog().Load("jebus-like");

        // Mutate exactly three top-level scalar fields.
        current.MapSize = preset.MapSize == 160 ? 200 : 160;
        current.PlayerCount = preset.PlayerCount == 2 ? 4 : 2;
        current.GenerateRoads = !preset.GenerateRoads;

        var rows = SettingsDiff.Compute(preset, current);

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, r => r.FieldPath == "mapSize");
        Assert.Contains(rows, r => r.FieldPath == "playerCount");
        Assert.Contains(rows, r => r.FieldPath == "generateRoads");
    }

    [Fact]
    public void Diff_Row_Carries_Both_Values_As_Strings()
    {
        var preset = new SettingsFile { MapSize = 160 };
        var current = new SettingsFile { MapSize = 200 };

        var rows = SettingsDiff.Compute(preset, current);

        var row = Assert.Single(rows, r => r.FieldPath == "mapSize");
        Assert.Equal("160", row.PresetValue);
        Assert.Equal("200", row.CurrentValue);
    }

    [Fact]
    public void Bool_Changes_Render_As_True_False()
    {
        var preset = new SettingsFile { GenerateRoads = true };
        var current = new SettingsFile { GenerateRoads = false };

        var rows = SettingsDiff.Compute(preset, current);

        var row = Assert.Single(rows, r => r.FieldPath == "generateRoads");
        Assert.Equal("true", row.PresetValue);
        Assert.Equal("false", row.CurrentValue);
    }

    [Fact]
    public void List_And_Dict_Differences_Are_Ignored()
    {
        // T-805 explicitly limits scope to top-level scalars; collection
        // properties (heroBans, bonusResources, contentCountLimits, …) must
        // not surface as diff rows even when they differ.
        var preset = new SettingsFile();
        preset.HeroBans.Add("hero_warlock");
        preset.BonusResources["gold"] = 10000;
        preset.ContentCountLimits.Add(new ContentLimitFile { Sid = "x", MaxPerPlayer = 1 });

        var current = new SettingsFile();
        // Different lists/dicts than preset, but same scalar fields.

        var rows = SettingsDiff.Compute(preset, current);

        Assert.DoesNotContain(rows, r => r.FieldPath == "heroBans");
        Assert.DoesNotContain(rows, r => r.FieldPath == "bonusResources");
        Assert.DoesNotContain(rows, r => r.FieldPath == "contentCountLimits");
        Assert.Empty(rows);
    }

    [Fact]
    public void Nullable_Unset_Vs_Set_Reports_Diff()
    {
        var preset = new SettingsFile { ResourceDensityPercent = null };
        var current = new SettingsFile { ResourceDensityPercent = 75 };

        var rows = SettingsDiff.Compute(preset, current);

        var row = Assert.Single(rows, r => r.FieldPath == "resourceDensity");
        Assert.Equal("(unset)", row.PresetValue);
        Assert.Equal("75", row.CurrentValue);
    }

    [Fact]
    public void Enum_Field_Renders_Member_Name()
    {
        var preset = new SettingsFile { Topology = MapTopology.Random };
        var current = new SettingsFile { Topology = MapTopology.Balanced };

        var rows = SettingsDiff.Compute(preset, current);

        var row = Assert.Single(rows, r => r.FieldPath == "topology");
        Assert.Equal("Random", row.PresetValue);
        Assert.Equal("Balanced", row.CurrentValue);
    }
}
