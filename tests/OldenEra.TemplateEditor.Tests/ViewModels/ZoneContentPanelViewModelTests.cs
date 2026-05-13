using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.TemplateEditor.ViewModels;

namespace OldenEra.TemplateEditor.Tests.ViewModels;

public class ZoneContentPanelViewModelTests
{
    private static ZoneContentItem Item(string sid, int min = 1, int max = 1) => new()
    {
        Sid = sid,
        MinCount = min,
        MaxCount = max,
    };

    private static GeneratorSettings BuildSettings(
        IEnumerable<ZoneContentItem>? player = null,
        IEnumerable<ZoneContentItem>? neutralGlobal = null,
        IDictionary<NeutralZoneTier, IEnumerable<ZoneContentItem>>? byTier = null,
        IDictionary<string, IEnumerable<ZoneContentItem>>? byLetter = null)
    {
        var s = new GeneratorSettings();
        if (player is not null)
            s.PlayerZoneContent = new ZoneContentList { Items = player.ToList() };
        if (neutralGlobal is not null)
            s.NeutralZoneContent.Global = new ZoneContentList { Items = neutralGlobal.ToList() };
        if (byTier is not null)
            foreach (var (k, v) in byTier)
                s.NeutralZoneContent.ByTier[k] = new ZoneContentList { Items = v.ToList() };
        if (byLetter is not null)
            foreach (var (k, v) in byLetter)
                s.NeutralZoneContent.ByZoneLetter[k] = new ZoneContentList { Items = v.ToList() };
        return s;
    }

    [Fact]
    public void Construction_PopulatesPlayerScopeAndExposesSixFixedScopes()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a"), Item("name_b") });

        var vm = new ZoneContentPanelViewModel(settings);

        Assert.Equal(2, vm.PlayerScope.Items.Count);
        Assert.Equal("name_a", vm.PlayerScope.Items[0].Sid);
        // Six fixed scopes: Player, NeutralGlobal, Poor, Normal, Rich (per-zone live in PerZoneScopes).
        Assert.Equal(5, vm.Scopes.Count);
        Assert.False(vm.IsDefaultsCompareActive);
        Assert.False(vm.IsReadOnly);
    }

    [Fact]
    public void PerZoneScopesOrdered_IsAlphabetical()
    {
        var settings = new GeneratorSettings();
        settings.NeutralZoneContent.ByZoneLetter["B"] = new ZoneContentList();
        settings.NeutralZoneContent.ByZoneLetter["A"] = new ZoneContentList();
        settings.NeutralZoneContent.ByZoneLetter["C"] = new ZoneContentList();
        var vm = new ZoneContentPanelViewModel(settings);
        Assert.Equal(new[] { "A", "B", "C" }, vm.PerZoneScopesOrdered.Select(kv => kv.Key).ToArray());
    }

    [Fact]
    public void Construction_PerZoneScopes_ExposeEachLetter()
    {
        var settings = BuildSettings(
            byLetter: new Dictionary<string, IEnumerable<ZoneContentItem>>
            {
                ["A"] = new[] { Item("name_a1") },
                ["B"] = new[] { Item("name_b1"), Item("name_b2") },
            });

        var vm = new ZoneContentPanelViewModel(settings);

        Assert.Equal(2, vm.PerZoneScopes.Count);
        Assert.True(vm.PerZoneScopes.ContainsKey("A"));
        Assert.True(vm.PerZoneScopes.ContainsKey("B"));
        Assert.Single(vm.PerZoneScopes["A"].Items);
        Assert.Equal(2, vm.PerZoneScopes["B"].Items.Count);
        Assert.Equal("name_b2", vm.PerZoneScopes["B"].Items[1].Sid);
    }

    [Fact]
    public void Presets_MatchesCatalog()
    {
        var vm = new ZoneContentPanelViewModel(new GeneratorSettings());

        var expected = ZoneContentPresets.All();
        Assert.Equal(expected.Count, vm.Presets.Count);
        for (var i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i].Name, vm.Presets[i].Name);
    }

    [Fact]
    public void Warnings_ProjectedOnConstruction()
    {
        // MinCount != MaxCount triggers MinCountRangeNarrowedToMax.
        var settings = BuildSettings(
            player: new[] { Item("name_a", min: 1, max: 3) });

        var vm = new ZoneContentPanelViewModel(settings);

        var expected = ZoneContentWarningProjection.Project(settings);
        Assert.Equal(expected.Count, vm.Warnings.Count);
        Assert.NotEmpty(vm.Warnings);
    }

    [Fact]
    public void DefaultsCompareOn_BlanksScopesAndFlipsReadOnly()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a"), Item("name_b") });
        var vm = new ZoneContentPanelViewModel(settings);

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        vm.IsDefaultsCompareActive = true;

        Assert.True(vm.IsDefaultsCompareActive);
        Assert.True(vm.IsReadOnly);
        Assert.Empty(vm.PlayerScope.Items);
        Assert.Contains(nameof(vm.IsDefaultsCompareActive), changed);
        Assert.Contains(nameof(vm.IsReadOnly), changed);
        Assert.Contains(nameof(vm.Settings), changed);
        Assert.Contains(nameof(vm.Warnings), changed);
    }

    [Fact]
    public void DefaultsCompareOff_RestoresOriginal()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a"), Item("name_b") });
        var vm = new ZoneContentPanelViewModel(settings);

        vm.IsDefaultsCompareActive = true;
        Assert.Empty(vm.PlayerScope.Items);

        vm.IsDefaultsCompareActive = false;

        Assert.False(vm.IsReadOnly);
        Assert.Same(settings, vm.Settings);
        Assert.Equal(2, vm.PlayerScope.Items.Count);
        Assert.Equal("name_a", vm.PlayerScope.Items[0].Sid);
    }

    [Fact]
    public void CommitToSettings_PushesScopeEditsBackAndFiresChanged()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a") });
        var vm = new ZoneContentPanelViewModel(settings);

        var fired = 0;
        vm.Changed += (_, _) => fired++;

        vm.PlayerScope.Items[0].Sid = "name_a_renamed";
        // Live-edit auto-commits on PropertyChanged; the explicit call below
        // performs an additional redundant commit. Exact fire count is not
        // part of the contract — assert at least one Changed fired.
        vm.CommitToSettings();

        Assert.Equal("name_a_renamed", settings.PlayerZoneContent.Items[0].Sid);
        Assert.True(fired >= 1);
    }

    [Fact]
    public void CommitToSettings_WhileReadOnly_IsNoOp()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a") });
        var vm = new ZoneContentPanelViewModel(settings);

        vm.IsDefaultsCompareActive = true;
        // Mutate the blanked clone — should not be written through.
        vm.PlayerScope.Items.Add(ZoneContentItemViewModel.FromModel(Item("blanked_intruder")));

        vm.CommitToSettings();

        // Toggle off and confirm original Settings retained.
        vm.IsDefaultsCompareActive = false;
        Assert.Single(settings.PlayerZoneContent.Items);
        Assert.Equal("name_a", settings.PlayerZoneContent.Items[0].Sid);
    }

    [Fact]
    public void CommitToSettings_DoesNotMaterializeEmptyAbsentTier()
    {
        // Settings without a Poor tier; PoorScope is empty and stays empty.
        // Empty absent tiers must remain absent so we don't pollute the
        // dictionary with empty lists on every commit.
        var settings = BuildSettings();
        var vm = new ZoneContentPanelViewModel(settings);

        Assert.Empty(vm.PoorScope.Items);

        vm.CommitToSettings();

        Assert.False(settings.NeutralZoneContent.ByTier.ContainsKey(NeutralZoneTier.Poor));
    }

    [Theory]
    [InlineData(NeutralZoneTier.Poor)]
    [InlineData(NeutralZoneTier.Normal)]
    [InlineData(NeutralZoneTier.Rich)]
    public void CommitToSettings_MaterializesAbsentTierOnFirstEdit(NeutralZoneTier tier)
    {
        // Settings without the tier; user adds an item via the empty scope.
        // Commit should materialize the dictionary entry so the edit
        // round-trips through Save/Open.
        var settings = BuildSettings();
        var vm = new ZoneContentPanelViewModel(settings);

        var scope = tier switch
        {
            NeutralZoneTier.Poor => vm.PoorScope,
            NeutralZoneTier.Normal => vm.NormalScope,
            NeutralZoneTier.Rich => vm.RichScope,
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };
        scope.Items.Add(ZoneContentItemViewModel.FromModel(Item("name_x")));
        // Live-edit auto-commits, but explicit call is the contract surface.
        vm.CommitToSettings();

        Assert.True(settings.NeutralZoneContent.ByTier.ContainsKey(tier));
        Assert.Single(settings.NeutralZoneContent.ByTier[tier].Items);
        Assert.Equal("name_x", settings.NeutralZoneContent.ByTier[tier].Items[0].Sid);
    }

    [Fact]
    public void CommitToSettings_WritesBackExistingPerZoneLetter()
    {
        var settings = BuildSettings(
            byLetter: new Dictionary<string, IEnumerable<ZoneContentItem>>
            {
                ["A"] = new[] { Item("name_a1") },
            });
        var vm = new ZoneContentPanelViewModel(settings);

        vm.PerZoneScopes["A"].Items[0].Sid = "name_a1_renamed";
        vm.CommitToSettings();

        Assert.Equal("name_a1_renamed", settings.NeutralZoneContent.ByZoneLetter["A"].Items[0].Sid);
    }

    [Fact]
    public void CommitToSettings_WritesExistingTier()
    {
        var settings = BuildSettings(
            byTier: new Dictionary<NeutralZoneTier, IEnumerable<ZoneContentItem>>
            {
                [NeutralZoneTier.Normal] = new[] { Item("name_n1") },
            });
        var vm = new ZoneContentPanelViewModel(settings);

        vm.NormalScope.Items[0].Sid = "name_n1_renamed";
        vm.CommitToSettings();

        Assert.Equal(
            "name_n1_renamed",
            settings.NeutralZoneContent.ByTier[NeutralZoneTier.Normal].Items[0].Sid);
    }

    [Fact]
    public void Warnings_AreDistributedToOwningItems()
    {
        // MinCount != MaxCount triggers MinCountRangeNarrowedToMax.
        var settings = BuildSettings(
            player: new[] { Item("name_a", min: 5, max: 1) });
        var vm = new ZoneContentPanelViewModel(settings);

        Assert.True(vm.PlayerScope.Items[0].WarningCount > 0);
        Assert.Equal(vm.PlayerScope.WarningCount, vm.PlayerScope.Items[0].WarningCount);
    }

    [Fact]
    public void DefaultsCompare_OnClearsItemWarnings()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a", min: 5, max: 1) });
        var vm = new ZoneContentPanelViewModel(settings);
        Assert.True(vm.PlayerScope.WarningCount > 0);

        vm.IsDefaultsCompareActive = true;

        // Blanked clone has no items.
        Assert.Empty(vm.PlayerScope.Items);
        Assert.Equal(0, vm.PlayerScope.WarningCount);
        Assert.False(vm.PlayerScope.HasWarnings);
    }

    [Fact]
    public void PerZoneWarningCount_AggregatesAcrossLetters()
    {
        var settings = BuildSettings(
            byLetter: new Dictionary<string, IEnumerable<ZoneContentItem>>
            {
                ["A"] = new[] { Item("name_a", min: 1, max: 3) },
                ["B"] = new[] { Item("name_b", min: 1, max: 4) },
            });
        var vm = new ZoneContentPanelViewModel(settings);

        Assert.True(vm.PerZoneWarningCount > 0);
        Assert.True(vm.PerZoneHasWarnings);
        Assert.Equal(
            vm.PerZoneScopes.Values.Sum(s => s.WarningCount),
            vm.PerZoneWarningCount);
    }

    [Fact]
    public void RoadDecorations_ReadsThroughLiveSettings()
    {
        var settings = new GeneratorSettings();
        settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration { Zone = "1" });

        var vm = new ZoneContentPanelViewModel(settings);

        Assert.Single(vm.RoadDecorations);
        Assert.Equal("1", vm.RoadDecorations[0].Zone);
    }

    [Fact]
    public void SidCatalog_MatchesStaticCatalog()
    {
        var vm = new ZoneContentPanelViewModel(new GeneratorSettings());
        Assert.Equal(ZoneContentSidCatalog.All(), vm.SidCatalog);
    }

    [Fact]
    public void PoolValues_ContainsAllPoolEnumMembers()
    {
        var vm = new ZoneContentPanelViewModel(new GeneratorSettings());
        Assert.Equal((ZoneContentPool[])Enum.GetValues(typeof(ZoneContentPool)), vm.PoolValues);
    }

    [Fact]
    public void ItemEdit_AfterCtor_RaisesChangedAndCommitsToSettings()
    {
        var settings = new GeneratorSettings();
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "old" });
        var vm = new ZoneContentPanelViewModel(settings);
        var changedFired = 0;
        vm.Changed += (_, _) => changedFired++;

        vm.PlayerScope.Items[0].Sid = "new";

        Assert.True(changedFired > 0);
        Assert.Equal("new", settings.PlayerZoneContent.Items[0].Sid);
    }

    [Fact]
    public void PresetAdd_RefreshesWarnings()
    {
        var vm = new ZoneContentPanelViewModel(new GeneratorSettings());
        var preset = ZoneContentPresets.All().First();
        vm.PlayerScope.AddPreset(preset);
        Assert.Single(vm.PlayerScope.Items);
        // Force an invalid edit to trigger a warning (Min > Max narrows to Max).
        vm.PlayerScope.Items[0].MinCount = 99;
        vm.PlayerScope.Items[0].MaxCount = 1;
        Assert.True(vm.PlayerScope.WarningCount > 0);
    }

    [Fact]
    public void DefaultsCompareToggle_PreservesOriginalSettings()
    {
        var settings = new GeneratorSettings();
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "original" });
        var vm = new ZoneContentPanelViewModel(settings);
        vm.IsDefaultsCompareActive = true;
        // Blanked clone has no items.
        Assert.Empty(vm.PlayerScope.Items);
        // Toggle off; original should be intact.
        vm.IsDefaultsCompareActive = false;
        Assert.Equal("original", settings.PlayerZoneContent.Items[0].Sid);
    }

    [Fact]
    public void DefaultsCompare_RoundTrip_WithEditsBetween()
    {
        // Edit while compare is OFF, toggle ON, toggle OFF — edits must persist.
        var settings = new GeneratorSettings();
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "v1" });
        var vm = new ZoneContentPanelViewModel(settings);

        vm.PlayerScope.Items[0].Sid = "v2";  // committed via OnLiveEdit
        Assert.Equal("v2", settings.PlayerZoneContent.Items[0].Sid);

        vm.IsDefaultsCompareActive = true;
        Assert.Empty(vm.PlayerScope.Items);

        vm.IsDefaultsCompareActive = false;
        Assert.Single(vm.PlayerScope.Items);
        Assert.Equal("v2", vm.PlayerScope.Items[0].Sid);
    }

    [Fact]
    public void RemoveItem_CommitsThroughToSettings()
    {
        var settings = BuildSettings(
            player: new[] { Item("name_a"), Item("name_b") });
        var vm = new ZoneContentPanelViewModel(settings);

        vm.PlayerScope.Items.RemoveAt(0);

        Assert.Single(settings.PlayerZoneContent.Items);
        Assert.Equal("name_b", settings.PlayerZoneContent.Items[0].Sid);
    }
}
