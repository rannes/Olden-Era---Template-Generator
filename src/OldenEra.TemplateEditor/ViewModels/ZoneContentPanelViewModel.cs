using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;

namespace OldenEra.TemplateEditor.ViewModels;

/// <summary>
/// Brain of the WPF zone-content panel. Exposes the working
/// <see cref="GeneratorSettings"/>, the per-scope item-list VMs, the
/// defaults-compare toggle, the projected warnings, and the curated preset
/// catalog. Mirrors the state machine used by
/// <c>OldenEra.Web/Components/ZoneContent/ZoneContentEditor.razor</c>.
/// </summary>
/// <remarks>
/// <para>Round 5 (v1) constraints:</para>
/// <list type="bullet">
///   <item><description>
///     <b>Conservative writes.</b> <see cref="CommitToSettings"/> only writes
///     back tier and per-zone-letter keys that already existed on the source
///     <see cref="GeneratorSettings"/> at construction time. The "auto-
///     materialize a new tier/letter on first edit" affordance is deferred.
///   </description></item>
///   <item><description>
///     <b>Road decorations.</b> Exposed read-through via
///     <see cref="RoadDecorations"/>; the dedicated WPF road-decorations
///     editor is a follow-up (out of A4 scope). They are NOT a sixth scope-VM
///     because <see cref="ZoneRoadDecoration"/> is not a
///     <see cref="ZoneContentItem"/>.
///   </description></item>
///   <item><description>
///     <b>Read-only.</b> While <see cref="IsDefaultsCompareActive"/> is true,
///     <see cref="CommitToSettings"/> is a no-op so the blanked view never
///     overwrites the live settings.
///   </description></item>
/// </list>
/// </remarks>
public sealed class ZoneContentPanelViewModel : INotifyPropertyChanged
{
    private readonly GeneratorSettings _originalSettings;
    private readonly IReadOnlyList<string> _originalLetters;
    private readonly IReadOnlySet<NeutralZoneTier> _originalTiers;

    private GeneratorSettings _settings;
    private bool _isDefaultsCompareActive;
    private IReadOnlyList<ZoneContentWarning> _warnings;
    private bool _suppressLiveEdit;

    private readonly ZoneContentScopeViewModel _player;
    private readonly ZoneContentScopeViewModel _neutralGlobal;
    private readonly ZoneContentScopeViewModel _poor;
    private readonly ZoneContentScopeViewModel _normal;
    private readonly ZoneContentScopeViewModel _rich;
    private readonly Dictionary<string, ZoneContentScopeViewModel> _perZone;

    public ZoneContentPanelViewModel(GeneratorSettings settings)
    {
        _originalSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings = settings;

        _originalLetters = settings.NeutralZoneContent.ByZoneLetter.Keys.ToList();
        _originalTiers = settings.NeutralZoneContent.ByTier.Keys.ToHashSet();

        _player = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.Player), "Player");
        _neutralGlobal = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.NeutralGlobal), "Neutral - Global");
        _poor = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPoor), "Poor");
        _normal = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.NeutralNormal), "Normal");
        _rich = new ZoneContentScopeViewModel(
            new ZoneContentScopeKey(ZoneContentScopeKind.NeutralRich), "Rich");

        _perZone = new Dictionary<string, ZoneContentScopeViewModel>(_originalLetters.Count);
        foreach (var letter in _originalLetters)
        {
            _perZone[letter] = new ZoneContentScopeViewModel(
                new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPerZone, letter),
                $"Per-zone {letter}");
        }

        // Deterministic ordinal-sorted snapshot for the WPF Per-zone tab list.
        // StringComparer.Ordinal matches Web optgroup ordering (Thread B reviewer note).
        PerZoneScopesOrdered = _perZone
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        Scopes = new[] { _player, _neutralGlobal, _poor, _normal, _rich };
        Presets = ZoneContentPresets.All();
        SidCatalog = ZoneContentSidCatalog.All();
        PoolValues = (ZoneContentPool[])Enum.GetValues(typeof(ZoneContentPool));

        RebuildScopeItems();
        _warnings = ZoneContentWarningProjection.Project(_settings);
        DistributeWarningsToItems();

        // Wire per-zone aggregate INPC. Per-zone scopes are not added/removed
        // after construction (deferred materialization) so a single subscription
        // per scope is sufficient.
        foreach (var scope in _perZone.Values)
            scope.PropertyChanged += OnPerZoneScopePropertyChanged;

        WireLiveEditSignals();
    }

    private IEnumerable<ZoneContentScopeViewModel> AllScopes()
    {
        yield return _player;
        yield return _neutralGlobal;
        yield return _poor;
        yield return _normal;
        yield return _rich;
        foreach (var s in _perZone.Values) yield return s;
    }

    // Reverse map from a scope's Items collection back to the scope, so the
    // CollectionChanged handler (whose sender is the collection, not the scope)
    // can resolve the owner without an O(n) scan.
    private readonly Dictionary<object, ZoneContentScopeViewModel> _scopeByItems = new();

    private void WireLiveEditSignals()
    {
        foreach (var scope in AllScopes())
        {
            _scopeByItems[scope.Items] = scope;
            scope.Items.CollectionChanged += OnScopeItemsChanged;
            foreach (var item in scope.Items)
                HookItemEdits(scope, item);
        }
    }

    // Tracks every item-VM whose PropertyChanged we've hooked, keyed by the
    // owning scope. Lets us unhook on Reset (Clear) where OldItems is null.
    private readonly Dictionary<ZoneContentScopeViewModel, HashSet<ZoneContentItemViewModel>>
        _hookedItems = new();

    private void HookItemEdits(ZoneContentScopeViewModel scope, ZoneContentItemViewModel item)
    {
        item.PropertyChanged += OnItemEdited;
        if (!_hookedItems.TryGetValue(scope, out var set))
            _hookedItems[scope] = set = new HashSet<ZoneContentItemViewModel>();
        set.Add(item);
    }

    private void UnhookItemEdits(ZoneContentScopeViewModel scope, ZoneContentItemViewModel item)
    {
        item.PropertyChanged -= OnItemEdited;
        if (_hookedItems.TryGetValue(scope, out var set))
            set.Remove(item);
    }

    private void OnScopeItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is null || !_scopeByItems.TryGetValue(sender, out var scope))
            return;
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Clear() raises Reset with OldItems == null — unhook everything we
            // had hooked under this scope so handlers don't leak.
            if (_hookedItems.TryGetValue(scope, out var set))
            {
                foreach (var vm in set)
                    vm.PropertyChanged -= OnItemEdited;
                set.Clear();
            }
            // Re-hook anything still present (defensive).
            foreach (var vm in scope.Items) HookItemEdits(scope, vm);
        }
        else
        {
            if (e.NewItems is not null)
                foreach (ZoneContentItemViewModel vm in e.NewItems)
                    HookItemEdits(scope, vm);
            if (e.OldItems is not null)
                foreach (ZoneContentItemViewModel vm in e.OldItems)
                    UnhookItemEdits(scope, vm);
        }
        OnLiveEdit();
    }

    private void OnItemEdited(object? sender, PropertyChangedEventArgs e)
    {
        // Filter out properties we set ourselves while distributing warnings,
        // otherwise CommitToSettings -> SetWarnings -> PropertyChanged would
        // reenter OnLiveEdit.
        if (e.PropertyName is nameof(ZoneContentItemViewModel.Warnings)
            or nameof(ZoneContentItemViewModel.WarningCount)
            or nameof(ZoneContentItemViewModel.HasWarnings)) return;
        OnLiveEdit();
    }

    private void OnLiveEdit()
    {
        if (IsReadOnly) return;
        if (_suppressLiveEdit) return;
        CommitToSettings();
    }

    public GeneratorSettings Settings => _settings;

    public IReadOnlyList<ZoneContentScopeViewModel> Scopes { get; }
    public ZoneContentScopeViewModel PlayerScope => _player;
    public ZoneContentScopeViewModel NeutralGlobalScope => _neutralGlobal;
    public ZoneContentScopeViewModel PoorScope => _poor;
    public ZoneContentScopeViewModel NormalScope => _normal;
    public ZoneContentScopeViewModel RichScope => _rich;
    public IReadOnlyDictionary<string, ZoneContentScopeViewModel> PerZoneScopes => _perZone;

    /// <summary>
    /// Per-zone scopes in deterministic ordinal-letter order, suitable for
    /// binding directly to the WPF Per-zone tab's letter list.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, ZoneContentScopeViewModel>> PerZoneScopesOrdered { get; }

    public IReadOnlyList<ZoneRoadDecoration> RoadDecorations => _settings.ZoneRoadDecorations;

    public IReadOnlyList<ZoneContentPreset> Presets { get; }

    public IReadOnlyList<ZoneContentSidEntry> SidCatalog { get; }

    public IReadOnlyList<ZoneContentPool> PoolValues { get; }

    public bool IsDefaultsCompareActive
    {
        get => _isDefaultsCompareActive;
        set
        {
            if (_isDefaultsCompareActive == value) return;
            _isDefaultsCompareActive = value;

            _settings = value
                ? ZoneContentCloning.CloneWithDefaultsBlanked(_originalSettings)
                : _originalSettings;

            RebuildScopeItems();
            _warnings = ZoneContentWarningProjection.Project(_settings);
            DistributeWarningsToItems();

            RaisePropertyChanged(nameof(IsDefaultsCompareActive));
            RaisePropertyChanged(nameof(IsReadOnly));
            RaisePropertyChanged(nameof(Settings));
            RaisePropertyChanged(nameof(Warnings));
        }
    }

    public bool IsReadOnly => _isDefaultsCompareActive;

    public IReadOnlyList<ZoneContentWarning> Warnings => _warnings;

    /// <summary>
    /// Aggregate warning count across every per-zone scope. Drives the Per-zone
    /// tab-header badge.
    /// </summary>
    public int PerZoneWarningCount => _perZone.Values.Sum(s => s.WarningCount);

    public bool PerZoneHasWarnings => PerZoneWarningCount > 0;

    public event EventHandler? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Pushes scope-VM edits back into the live <see cref="Settings"/>.
    /// No-op while <see cref="IsReadOnly"/> is true. Conservative writes:
    /// only re-writes tier/per-zone keys that already existed at
    /// construction time.
    /// </summary>
    public void CommitToSettings()
    {
        if (IsReadOnly) return;

        _originalSettings.PlayerZoneContent.Items = _player.ToModels().ToList();
        _originalSettings.NeutralZoneContent.Global.Items = _neutralGlobal.ToModels().ToList();

        WriteTierIfPresent(NeutralZoneTier.Poor, _poor);
        WriteTierIfPresent(NeutralZoneTier.Normal, _normal);
        WriteTierIfPresent(NeutralZoneTier.Rich, _rich);

        foreach (var letter in _originalLetters)
        {
            if (!_originalSettings.NeutralZoneContent.ByZoneLetter.TryGetValue(letter, out var list))
                continue;
            if (!_perZone.TryGetValue(letter, out var scope)) continue;
            list.Items = scope.ToModels().ToList();
        }

        _warnings = ZoneContentWarningProjection.Project(_settings);
        DistributeWarningsToItems();
        RaisePropertyChanged(nameof(Warnings));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void WriteTierIfPresent(NeutralZoneTier tier, ZoneContentScopeViewModel scope)
    {
        if (!_originalTiers.Contains(tier)) return;
        if (!_originalSettings.NeutralZoneContent.ByTier.TryGetValue(tier, out var list)) return;
        list.Items = scope.ToModels().ToList();
    }

    private void RebuildScopeItems()
    {
        var prev = _suppressLiveEdit;
        _suppressLiveEdit = true;
        try
        {
            ReplaceItems(_player, _settings.PlayerZoneContent.Items);
            ReplaceItems(_neutralGlobal, _settings.NeutralZoneContent.Global.Items);
            ReplaceItems(_poor, ItemsForTier(NeutralZoneTier.Poor));
            ReplaceItems(_normal, ItemsForTier(NeutralZoneTier.Normal));
            ReplaceItems(_rich, ItemsForTier(NeutralZoneTier.Rich));

            foreach (var letter in _originalLetters)
            {
                IEnumerable<ZoneContentItem> items =
                    _settings.NeutralZoneContent.ByZoneLetter.TryGetValue(letter, out var list)
                        ? list.Items
                        : Array.Empty<ZoneContentItem>();
                ReplaceItems(_perZone[letter], items);
            }
        }
        finally
        {
            _suppressLiveEdit = prev;
        }
    }

    private IReadOnlyList<ZoneContentItem> ItemsForTier(NeutralZoneTier tier) =>
        _settings.NeutralZoneContent.ByTier.TryGetValue(tier, out var list)
            ? (IReadOnlyList<ZoneContentItem>)list.Items
            : Array.Empty<ZoneContentItem>();

    private static void ReplaceItems(ZoneContentScopeViewModel scope, IEnumerable<ZoneContentItem> items)
    {
        scope.Items.Clear();
        foreach (var item in items)
            scope.Items.Add(ZoneContentItemViewModel.FromModel(item));
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Distributes the flat <see cref="Warnings"/> list down to each
    /// item-VM by joining on <c>(scope, handle ?? "#index")</c>. Mirrors the
    /// Web projection in <c>ZoneContentEditor.razor</c>.
    /// </summary>
    private void DistributeWarningsToItems()
    {
        var byScope = _warnings
            .GroupBy(w => w.Scope)
            .ToDictionary(g => g.Key, g => g.ToList());

        DistributeForScope(byScope, new ZoneContentScopeKey(ZoneContentScopeKind.Player), _player);
        DistributeForScope(byScope, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralGlobal), _neutralGlobal);
        DistributeForScope(byScope, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPoor), _poor);
        DistributeForScope(byScope, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralNormal), _normal);
        DistributeForScope(byScope, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralRich), _rich);
        foreach (var (letter, scope) in _perZone)
            DistributeForScope(byScope, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPerZone, letter), scope);
    }

    private static void DistributeForScope(
        Dictionary<ZoneContentScopeKey, List<ZoneContentWarning>> byScope,
        ZoneContentScopeKey key,
        ZoneContentScopeViewModel scope)
    {
        if (!byScope.TryGetValue(key, out var bag))
        {
            for (var i = 0; i < scope.Items.Count; i++)
                scope.Items[i].SetWarnings(Array.Empty<EmitWarning>());
            return;
        }
        for (var i = 0; i < scope.Items.Count; i++)
        {
            var item = scope.Items[i];
            var idKey = string.IsNullOrEmpty(item.HandleText) ? $"#{i}" : item.HandleText;
            var matching = bag
                .Where(w => (string.IsNullOrEmpty(w.Handle) ? $"#{w.ItemIndex}" : w.Handle) == idKey)
                .Select(w => w.Warning)
                .ToList();
            item.SetWarnings(matching);
        }
    }

    private void OnPerZoneScopePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ZoneContentScopeViewModel.WarningCount))
        {
            RaisePropertyChanged(nameof(PerZoneWarningCount));
            RaisePropertyChanged(nameof(PerZoneHasWarnings));
        }
    }
}
