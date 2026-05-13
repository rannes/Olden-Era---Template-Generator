using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;
using OldenEra.TemplateEditor.ViewModels;

namespace OldenEra.TemplateEditor.Tests.ViewModels;

public class ZoneContentScopeViewModelTests
{
    private static ZoneContentItem Item(string sid, int min = 1, int max = 1) => new()
    {
        Sid = sid,
        MinCount = min,
        MaxCount = max,
    };

    [Fact]
    public void Ctor_SetsKeyAndLabel_AndItemsIsEmpty()
    {
        var key = new ZoneContentScopeKey(ZoneContentScopeKind.Player);

        var vm = new ZoneContentScopeViewModel(key, "Player");

        Assert.Equal(key, vm.Key);
        Assert.Equal("Player", vm.Label);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void From_PopulatesItemsInOrder()
    {
        var key = new ZoneContentScopeKey(ZoneContentScopeKind.NeutralGlobal);
        var items = new[]
        {
            Item("name_a", min: 1, max: 2),
            Item("name_b", min: 3, max: 4),
            Item("name_c", min: 5, max: 6),
        };

        var vm = ZoneContentScopeViewModel.From(key, "Neutral · Global", items);

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal("name_a", vm.Items[0].Sid);
        Assert.Equal(1, vm.Items[0].MinCount);
        Assert.Equal("name_b", vm.Items[1].Sid);
        Assert.Equal("name_c", vm.Items[2].Sid);
        Assert.Equal(6, vm.Items[2].MaxCount);
    }

    [Fact]
    public void ToModels_RoundTripsAndReflectsMutations()
    {
        var key = new ZoneContentScopeKey(ZoneContentScopeKind.NeutralNormal);
        var items = new[]
        {
            Item("name_a", min: 1, max: 2),
            Item("name_b", min: 3, max: 4),
        };

        var vm = ZoneContentScopeViewModel.From(key, "Normal", items);

        var firstPass = vm.ToModels();
        Assert.Equal(2, firstPass.Count);
        Assert.Equal("name_a", firstPass[0].Sid);
        Assert.Equal(3, firstPass[1].MinCount);

        vm.Items[0].Sid = "name_a_renamed";

        var secondPass = vm.ToModels();
        Assert.Equal("name_a_renamed", secondPass[0].Sid);
        Assert.Equal("name_b", secondPass[1].Sid);
    }

    [Fact]
    public void ItemsAdd_RaisesCollectionChanged()
    {
        var vm = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player");
        NotifyCollectionChangedEventArgs? captured = null;
        vm.Items.CollectionChanged += (_, e) => captured = e;

        vm.Items.Add(ZoneContentItemViewModel.FromModel(Item("x")));

        Assert.NotNull(captured);
        Assert.Equal(NotifyCollectionChangedAction.Add, captured!.Action);
    }

    [Fact]
    public void ItemsRemove_RaisesCollectionChanged()
    {
        var vm = ZoneContentScopeViewModel.From(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player),
            "Player",
            new[] { Item("x") });
        NotifyCollectionChangedEventArgs? captured = null;
        vm.Items.CollectionChanged += (_, e) => captured = e;

        vm.Items.RemoveAt(0);

        Assert.NotNull(captured);
        Assert.Equal(NotifyCollectionChangedAction.Remove, captured!.Action);
    }

    [Fact]
    public void WarningCount_AggregatesAcrossItems()
    {
        var scope = ZoneContentScopeViewModel.From(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player",
            new[] { Item("a"), Item("b") });

        scope.Items[0].SetWarnings(new[] { new EmitWarning("X", "m", null, null) });
        scope.Items[1].SetWarnings(new[]
        {
            new EmitWarning("X", "m", null, null),
            new EmitWarning("Y", "m", null, null),
        });

        Assert.Equal(3, scope.WarningCount);
        Assert.True(scope.HasWarnings);
    }

    [Fact]
    public void WarningCount_RaisesPropertyChangedOnItemWarningChange()
    {
        var scope = ZoneContentScopeViewModel.From(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player",
            new[] { Item("a") });
        var raised = new List<string>();
        ((INotifyPropertyChanged)scope).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        scope.Items[0].SetWarnings(new[] { new EmitWarning("X", "m", null, null) });

        Assert.Contains(nameof(scope.WarningCount), raised);
        Assert.Contains(nameof(scope.HasWarnings), raised);
    }

    [Fact]
    public void WarningCount_RaisesPropertyChangedOnAddRemove()
    {
        var scope = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player");
        var raised = new List<string>();
        ((INotifyPropertyChanged)scope).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        scope.Items.Add(ZoneContentItemViewModel.FromModel(Item("x")));

        Assert.Contains(nameof(scope.WarningCount), raised);
    }

    [Fact]
    public void AddPreset_AppendsClonedItem()
    {
        var preset = ZoneContentPresets.All().First();
        var scope = ZoneContentScopeViewModel.From(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player",
            Array.Empty<ZoneContentItem>());

        scope.AddPreset(preset);

        Assert.Single(scope.Items);
        Assert.Equal(preset.Item.Sid, scope.Items[0].Sid);

        // Mutating the scope item must not affect the preset's stored Item.
        scope.Items[0].Sid = "mutated";
        Assert.Equal(preset.Item.Sid, ZoneContentPresets.All().First().Item.Sid);
    }

    [Fact]
    public void Items_Clear_UnsubscribesAllItems()
    {
        var scope = ZoneContentScopeViewModel.From(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player",
            new[] { new ZoneContentItem { Sid = "a" }, new ZoneContentItem { Sid = "b" } });
        var item0 = scope.Items[0];
        var item1 = scope.Items[1];
        int notifications = 0;
        ((INotifyPropertyChanged)scope).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ZoneContentScopeViewModel.WarningCount)) notifications++;
        };

        scope.Items.Clear();
        notifications = 0;

        // After Clear, the scope must not react to events from the removed items.
        item0.SetWarnings(new[] { new EmitWarning("X", "m", null, null) });
        item1.SetWarnings(new[] { new EmitWarning("Y", "m", null, null) });
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Key_WithZoneLetter_RoundTrips()
    {
        var key = new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPerZone, "B");

        var vm = new ZoneContentScopeViewModel(key, "Per-zone B");

        Assert.Equal(ZoneContentScopeKind.NeutralPerZone, vm.Key.Kind);
        Assert.Equal("B", vm.Key.ZoneLetter);
        Assert.Equal(key, vm.Key);
    }
}
