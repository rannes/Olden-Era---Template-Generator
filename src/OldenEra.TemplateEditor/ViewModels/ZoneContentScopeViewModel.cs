using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;

namespace OldenEra.TemplateEditor.ViewModels;

/// <summary>
/// Holds the item-VMs for a single zone-content scope (player, neutral global,
/// per-zone, etc.). The scope's identity lives in <see cref="Key"/> so the
/// per-zone letter rides along with the kind. <see cref="Label"/> is supplied
/// by the panel-VM so labelling stays a presentation concern.
/// </summary>
public sealed class ZoneContentScopeViewModel
{
    public ZoneContentScopeKey Key { get; }
    public string Label { get; }
    public ObservableCollection<ZoneContentItemViewModel> Items { get; } = new();

    public ZoneContentScopeViewModel(ZoneContentScopeKey key, string label)
    {
        Key = key;
        Label = label;
    }

    public static ZoneContentScopeViewModel From(
        ZoneContentScopeKey key,
        string label,
        IEnumerable<ZoneContentItem> items)
    {
        var vm = new ZoneContentScopeViewModel(key, label);
        foreach (var item in items)
            vm.Items.Add(ZoneContentItemViewModel.FromModel(item));
        return vm;
    }

    public IReadOnlyList<ZoneContentItem> ToModels() =>
        Items.Select(i => i.ToModel()).ToList();
}
