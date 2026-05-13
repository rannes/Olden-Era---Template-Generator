using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
/// <remarks>
/// Implements <see cref="INotifyPropertyChanged"/> so the WPF tab-header
/// badges can react to <see cref="WarningCount"/> changes. The scope subscribes
/// to each item's <see cref="ZoneContentItemViewModel.PropertyChanged"/> and
/// re-fires <c>WarningCount</c>/<c>HasWarnings</c> when an item's count changes
/// or when items are added/removed.
/// </remarks>
public sealed class ZoneContentScopeViewModel : INotifyPropertyChanged
{
    public ZoneContentScopeKey Key { get; }
    public string Label { get; }
    public ObservableCollection<ZoneContentItemViewModel> Items { get; } = new();

    public ZoneContentScopeViewModel(ZoneContentScopeKey key, string label)
    {
        Key = key;
        Label = label;
        Items.CollectionChanged += OnItemsChanged;
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

    /// <summary>
    /// Appends a fresh item-VM seeded from <paramref name="preset"/>. The
    /// preset's stored DTO is deep-cloned so subsequent edits never alias
    /// back into the curated catalog.
    /// </summary>
    public void AddPreset(ZoneContentPreset preset)
    {
        var clone = ZoneContentCloning.CloneItem(preset.Item);
        Items.Add(ZoneContentItemViewModel.FromModel(clone));
    }

    public int WarningCount => Items.Sum(i => i.WarningCount);

    public bool HasWarnings => WarningCount > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (ZoneContentItemViewModel vm in e.NewItems)
                vm.PropertyChanged += OnItemPropertyChanged;
        if (e.OldItems is not null)
            foreach (ZoneContentItemViewModel vm in e.OldItems)
                vm.PropertyChanged -= OnItemPropertyChanged;
        RaiseWarningCountChanged();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ZoneContentItemViewModel.WarningCount))
            RaiseWarningCountChanged();
    }

    private void RaiseWarningCountChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasWarnings)));
    }
}
