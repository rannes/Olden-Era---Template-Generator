using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.TemplateEditor.ViewModels;

namespace OldenEra.TemplateEditor.Tests.ViewModels;

public class ZoneContentItemViewModelTests
{
    private static ZoneContentItem FullyPopulated() => new()
    {
        Sid = "name_mana_well",
        Handle = "mana1",
        IsGroup = true,
        MinCount = 2,
        MaxCount = 5,
        Pool = ZoneContentPool.Guarded,
        IsGuarded = true,
        NearCastle = true,
        RoadDistance = RoadDistance.Mid,
        FactionAffinity = new List<string> { "haven", "academy" },
        BiomeFilter = new List<string> { "forest", "snow" },
    };

    [Fact]
    public void FromModel_PopulatesAllProperties()
    {
        var model = FullyPopulated();

        var vm = ZoneContentItemViewModel.FromModel(model);

        Assert.Equal("name_mana_well", vm.Sid);
        Assert.Equal("mana1", vm.HandleText);
        Assert.True(vm.IsGroup);
        Assert.Equal(2, vm.MinCount);
        Assert.Equal(5, vm.MaxCount);
        Assert.Equal(ZoneContentPool.Guarded, vm.Pool);
        Assert.True(vm.IsGuarded);
        Assert.True(vm.NearCastle);
        Assert.Equal(RoadDistance.Mid, vm.RoadDistance);
        Assert.Equal("haven, academy", vm.FactionAffinityCsv);
        Assert.Equal("forest, snow", vm.BiomeFilterCsv);
    }

    [Fact]
    public void FromModel_DefaultItem_HasEmptyHandleText()
    {
        var model = new ZoneContentItem();

        var vm = ZoneContentItemViewModel.FromModel(model);

        Assert.Equal("", vm.Sid);
        Assert.Equal("", vm.HandleText);
        Assert.False(vm.IsGroup);
        Assert.Equal(1, vm.MinCount);
        Assert.Equal(1, vm.MaxCount);
        Assert.Equal(ZoneContentPool.Mandatory, vm.Pool);
        Assert.False(vm.IsGuarded);
        Assert.False(vm.NearCastle);
        Assert.Null(vm.RoadDistance);
        Assert.Equal("", vm.FactionAffinityCsv);
        Assert.Equal("", vm.BiomeFilterCsv);
    }

    [Fact]
    public void ToModel_RoundTripsAllFields()
    {
        var original = FullyPopulated();

        var roundTripped = ZoneContentItemViewModel.FromModel(original).ToModel();

        Assert.Equal(original.Sid, roundTripped.Sid);
        Assert.Equal(original.Handle, roundTripped.Handle);
        Assert.Equal(original.IsGroup, roundTripped.IsGroup);
        Assert.Equal(original.MinCount, roundTripped.MinCount);
        Assert.Equal(original.MaxCount, roundTripped.MaxCount);
        Assert.Equal(original.Pool, roundTripped.Pool);
        Assert.Equal(original.IsGuarded, roundTripped.IsGuarded);
        Assert.Equal(original.NearCastle, roundTripped.NearCastle);
        Assert.Equal(original.RoadDistance, roundTripped.RoadDistance);
        Assert.Equal(original.FactionAffinity, roundTripped.FactionAffinity);
        Assert.Equal(original.BiomeFilter, roundTripped.BiomeFilter);
    }

    [Fact]
    public void ToModel_EmptyHandleText_BecomesNullHandle()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem { Handle = "x" });
        vm.HandleText = "";

        var model = vm.ToModel();

        Assert.Null(model.Handle);
    }

    [Fact]
    public void ToModel_WhitespaceHandleText_BecomesNullHandle()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        vm.HandleText = "   ";

        var model = vm.ToModel();

        Assert.Null(model.Handle);
    }

    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("a, b , c", new[] { "a", "b", "c" })]
    [InlineData("a,,b", new[] { "a", "b" })]
    [InlineData("a,b,", new[] { "a", "b" })]
    [InlineData(",a,b", new[] { "a", "b" })]
    [InlineData("solo", new[] { "solo" })]
    public void FactionAffinityCsv_Set_ParsesCsv(string csv, string[] expected)
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());

        vm.FactionAffinityCsv = csv;

        var model = vm.ToModel();
        Assert.Equal(expected, model.FactionAffinity);
    }

    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("a, b , c", new[] { "a", "b", "c" })]
    [InlineData("a,,b", new[] { "a", "b" })]
    [InlineData("a,b,", new[] { "a", "b" })]
    public void BiomeFilterCsv_Set_ParsesCsv(string csv, string[] expected)
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());

        vm.BiomeFilterCsv = csv;

        var model = vm.ToModel();
        Assert.Equal(expected, model.BiomeFilter);
    }

    [Fact]
    public void FactionAffinityCsv_Get_JoinsWithCommaSpace()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem
        {
            FactionAffinity = new List<string> { "x", "y" },
        });

        Assert.Equal("x, y", vm.FactionAffinityCsv);
    }

    [Fact]
    public void BiomeFilterCsv_Get_JoinsWithCommaSpace()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem
        {
            BiomeFilter = new List<string> { "lava", "ocean" },
        });

        Assert.Equal("lava, ocean", vm.BiomeFilterCsv);
    }

    [Fact]
    public void MinCount_ChangedValue_RaisesPropertyChanged()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem { MinCount = 1 });
        var raised = TrackPropertyChanges(vm);

        vm.MinCount = 4;

        Assert.Contains(nameof(vm.MinCount), raised);
    }

    [Fact]
    public void MinCount_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem { MinCount = 3 });
        var raised = TrackPropertyChanges(vm);

        vm.MinCount = 3;

        Assert.DoesNotContain(nameof(vm.MinCount), raised);
    }

    [Fact]
    public void Sid_ChangedValue_RaisesPropertyChanged()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem { Sid = "a" });
        var raised = TrackPropertyChanges(vm);

        vm.Sid = "b";

        Assert.Contains(nameof(vm.Sid), raised);
    }

    [Fact]
    public void Sid_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem { Sid = "a" });
        var raised = TrackPropertyChanges(vm);

        vm.Sid = "a";

        Assert.DoesNotContain(nameof(vm.Sid), raised);
    }

    [Fact]
    public void FactionAffinityCsv_ChangedValue_RaisesPropertyChanged()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        var raised = TrackPropertyChanges(vm);

        vm.FactionAffinityCsv = "haven";

        Assert.Contains(nameof(vm.FactionAffinityCsv), raised);
    }

    [Fact]
    public void FactionAffinityCsv_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem
        {
            FactionAffinity = new List<string> { "haven" },
        });
        var raised = TrackPropertyChanges(vm);

        vm.FactionAffinityCsv = "haven";

        Assert.DoesNotContain(nameof(vm.FactionAffinityCsv), raised);
    }

    [Fact]
    public void SetWarnings_RaisesPropertyChangedForAllDerived()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        var raised = TrackPropertyChanges(vm);

        vm.SetWarnings(new[] { new EmitWarning("X", "msg", null, null) });

        Assert.Contains(nameof(vm.Warnings), raised);
        Assert.Contains(nameof(vm.WarningCount), raised);
        Assert.Contains(nameof(vm.HasWarnings), raised);
        Assert.True(vm.HasWarnings);
        Assert.Equal(1, vm.WarningCount);
    }

    [Fact]
    public void WarningsFor_FiltersByCode()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        vm.SetWarnings(new[]
        {
            new EmitWarning(EmitWarning.Codes.PoolNonMandatoryDropped, "pool", null, null),
            new EmitWarning(EmitWarning.Codes.MinCountRangeNarrowedToMax, "minmax", null, null),
            new EmitWarning(EmitWarning.Codes.PoolNonMandatoryDropped, "pool2", null, null),
        });

        var pool = vm.WarningsFor(EmitWarning.Codes.PoolNonMandatoryDropped);
        Assert.Equal(2, pool.Count);
        Assert.All(pool, w => Assert.Equal(EmitWarning.Codes.PoolNonMandatoryDropped, w.Code));

        var minmax = vm.WarningsFor(EmitWarning.Codes.MinCountRangeNarrowedToMax);
        Assert.Single(minmax);
    }

    [Fact]
    public void PerFieldBadgeProjections_MatchUnderlyingCodes()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        vm.SetWarnings(new[]
        {
            new EmitWarning(EmitWarning.Codes.MinCountRangeNarrowedToMax, "m", null, null),
            new EmitWarning(EmitWarning.Codes.PoolNonMandatoryDropped, "p", null, null),
            new EmitWarning(EmitWarning.Codes.FactionAffinityIgnored, "f", null, null),
            new EmitWarning(EmitWarning.Codes.BiomeFilterIgnored, "b", null, null),
        });

        Assert.Single(vm.MinMaxWarnings);
        Assert.Single(vm.PoolWarnings);
        Assert.Single(vm.FactionAffinityWarnings);
        Assert.Single(vm.BiomeFilterWarnings);
        Assert.True(vm.HasMinMaxWarnings);
        Assert.True(vm.HasPoolWarnings);
        Assert.True(vm.HasFactionAffinityWarnings);
        Assert.True(vm.HasBiomeFilterWarnings);
    }

    [Fact]
    public void SetWarnings_RaisesPropertyChangedForPerFieldProjections()
    {
        var vm = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        var raised = TrackPropertyChanges(vm);

        vm.SetWarnings(new[]
        {
            new EmitWarning(EmitWarning.Codes.PoolNonMandatoryDropped, "p", null, null),
        });

        Assert.Contains(nameof(vm.MinMaxWarnings), raised);
        Assert.Contains(nameof(vm.PoolWarnings), raised);
        Assert.Contains(nameof(vm.FactionAffinityWarnings), raised);
        Assert.Contains(nameof(vm.BiomeFilterWarnings), raised);
        Assert.Contains(nameof(vm.HasMinMaxWarnings), raised);
        Assert.Contains(nameof(vm.HasPoolWarnings), raised);
        Assert.Contains(nameof(vm.HasFactionAffinityWarnings), raised);
        Assert.Contains(nameof(vm.HasBiomeFilterWarnings), raised);
    }

    [Fact]
    public void KeyForIndex_PrefersHandle_FallsBackToIndex()
    {
        var withHandle = ZoneContentItemViewModel.FromModel(new ZoneContentItem { Handle = "h1" });
        Assert.Equal("h1", withHandle.KeyForIndex(7));

        var noHandle = ZoneContentItemViewModel.FromModel(new ZoneContentItem());
        Assert.Equal("#3", noHandle.KeyForIndex(3));
    }

    private static List<string> TrackPropertyChanges(INotifyPropertyChanged target)
    {
        var raised = new List<string>();
        target.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null) raised.Add(e.PropertyName);
        };
        return raised;
    }
}
