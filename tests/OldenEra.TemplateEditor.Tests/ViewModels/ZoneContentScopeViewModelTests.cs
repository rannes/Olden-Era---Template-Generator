using System.Collections.Generic;
using System.Collections.Specialized;
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
    public void Key_WithZoneLetter_RoundTrips()
    {
        var key = new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPerZone, "B");

        var vm = new ZoneContentScopeViewModel(key, "Per-zone B");

        Assert.Equal(ZoneContentScopeKind.NeutralPerZone, vm.Key.Kind);
        Assert.Equal("B", vm.Key.ZoneLetter);
        Assert.Equal(key, vm.Key);
    }
}
