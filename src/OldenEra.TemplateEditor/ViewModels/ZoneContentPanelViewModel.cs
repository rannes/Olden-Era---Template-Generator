using System;
using System.Collections.Generic;
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

        Scopes = new[] { _player, _neutralGlobal, _poor, _normal, _rich };
        Presets = ZoneContentPresets.All();

        RebuildScopeItems();
        _warnings = ZoneContentWarningProjection.Project(_settings);
    }

    public GeneratorSettings Settings => _settings;

    public IReadOnlyList<ZoneContentScopeViewModel> Scopes { get; }
    public ZoneContentScopeViewModel PlayerScope => _player;
    public ZoneContentScopeViewModel NeutralGlobalScope => _neutralGlobal;
    public ZoneContentScopeViewModel PoorScope => _poor;
    public ZoneContentScopeViewModel NormalScope => _normal;
    public ZoneContentScopeViewModel RichScope => _rich;
    public IReadOnlyDictionary<string, ZoneContentScopeViewModel> PerZoneScopes => _perZone;

    public IReadOnlyList<ZoneRoadDecoration> RoadDecorations => _settings.ZoneRoadDecorations;

    public IReadOnlyList<ZoneContentPreset> Presets { get; }

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

            RaisePropertyChanged(nameof(IsDefaultsCompareActive));
            RaisePropertyChanged(nameof(IsReadOnly));
            RaisePropertyChanged(nameof(Settings));
            RaisePropertyChanged(nameof(Warnings));
        }
    }

    public bool IsReadOnly => _isDefaultsCompareActive;

    public IReadOnlyList<ZoneContentWarning> Warnings => _warnings;

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
}
