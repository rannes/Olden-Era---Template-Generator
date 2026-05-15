using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services.ZoneContent;

namespace OldenEra.TemplateEditor.ViewModels;

/// <summary>
/// INPC wrapper around <see cref="ZoneContentItem"/>. Exposes every model field plus
/// CSV string proxies for the list-of-string fields and a HandleText proxy that
/// round-trips empty/whitespace -&gt; null on the underlying Handle.
/// </summary>
public sealed class ZoneContentItemViewModel : INotifyPropertyChanged
{
    private string _sid = "";
    private string _handleText = "";
    private bool _isGroup;
    private int _minCount = 1;
    private int _maxCount = 1;
    private ZoneContentPool _pool = ZoneContentPool.Mandatory;
    private bool _isGuarded;
    private bool _nearCastle;
    private RoadDistance? _roadDistance;
    private string _factionAffinityCsv = "";
    private string _biomeFilterCsv = "";
    private string _rulesCsv = "";
    private string _includeListIdsCsv = "";
    private IReadOnlyList<EmitWarning> _warnings = Array.Empty<EmitWarning>();
    private IReadOnlyList<EmitWarning> _minMaxWarnings = Array.Empty<EmitWarning>();
    private IReadOnlyList<EmitWarning> _poolWarnings = Array.Empty<EmitWarning>();
    private IReadOnlyList<EmitWarning> _factionAffinityWarnings = Array.Empty<EmitWarning>();
    private IReadOnlyList<EmitWarning> _biomeFilterWarnings = Array.Empty<EmitWarning>();

    public string Sid
    {
        get => _sid;
        set => SetField(ref _sid, value);
    }

    public string HandleText
    {
        get => _handleText;
        set => SetField(ref _handleText, value);
    }

    public bool IsGroup
    {
        get => _isGroup;
        set => SetField(ref _isGroup, value);
    }

    public int MinCount
    {
        get => _minCount;
        set => SetField(ref _minCount, value);
    }

    public int MaxCount
    {
        get => _maxCount;
        set => SetField(ref _maxCount, value);
    }

    public ZoneContentPool Pool
    {
        get => _pool;
        set => SetField(ref _pool, value);
    }

    public bool IsGuarded
    {
        get => _isGuarded;
        set => SetField(ref _isGuarded, value);
    }

    public bool NearCastle
    {
        get => _nearCastle;
        set => SetField(ref _nearCastle, value);
    }

    public RoadDistance? RoadDistance
    {
        get => _roadDistance;
        set => SetField(ref _roadDistance, value);
    }

    public string FactionAffinityCsv
    {
        get => _factionAffinityCsv;
        set => SetField(ref _factionAffinityCsv, value);
    }

    public string BiomeFilterCsv
    {
        get => _biomeFilterCsv;
        set => SetField(ref _biomeFilterCsv, value);
    }

    /// <summary>
    /// Rules in single-line CSV form (T-202). Format:
    /// <c>Type|args1/args2|min|max|weight; Type|...</c>. Empty / whitespace
    /// = no rules. Round-trips through <see cref="ZoneContentRuleCsv"/>.
    /// </summary>
    public string RulesCsv
    {
        get => _rulesCsv;
        set => SetField(ref _rulesCsv, value);
    }

    /// <summary>
    /// Catalog-picked content-list IDs (T-605) flattened to a CSV string
    /// for the WPF row-template binding. Format: <c>id_one, id_two</c>;
    /// empty / whitespace = no <c>includeLists</c> emitted. Round-trips
    /// through <see cref="FromModel"/> / <see cref="ToModel"/>. Catalog
    /// validation lives in the picker dropdown — raw text typed here is
    /// preserved verbatim so author-edited templates with non-catalog IDs
    /// still round-trip cleanly.
    /// </summary>
    public string IncludeListIdsCsv
    {
        get => _includeListIdsCsv;
        set => SetField(ref _includeListIdsCsv, value);
    }

    /// <summary>
    /// Warnings projected for this item. Populated by the panel VM after every
    /// warnings refresh; consumers are read-only. Drives per-row badge display.
    /// </summary>
    public IReadOnlyList<EmitWarning> Warnings => _warnings;

    public int WarningCount => _warnings.Count;

    public bool HasWarnings => _warnings.Count > 0;

    /// <summary>
    /// Filters <see cref="Warnings"/> to entries whose <c>Code</c> equals
    /// <paramref name="code"/>. Mirrors the Web's <c>WarningsFor</c> helper in
    /// <c>ZoneContentItemDetail.razor</c>. The named projections below
    /// (<see cref="MinMaxWarnings"/>, <see cref="PoolWarnings"/>, etc.) cache
    /// the codes that have a UI surface today; prefer those over calling this
    /// helper from a binding hot path.
    /// </summary>
    public IReadOnlyList<EmitWarning> WarningsFor(string code) =>
        _warnings.Where(w => w.Code == code).ToList();

    /// <summary>
    /// Warnings shown by the badge that sits next to the Min/Max counts pair
    /// (the underlying <c>MinCountRangeNarrowedToMax</c> code spans both
    /// fields, hence the pair-cell name rather than a single-field name).
    /// </summary>
    public IReadOnlyList<EmitWarning> MinMaxWarnings => _minMaxWarnings;

    public IReadOnlyList<EmitWarning> PoolWarnings => _poolWarnings;

    public IReadOnlyList<EmitWarning> FactionAffinityWarnings => _factionAffinityWarnings;

    public IReadOnlyList<EmitWarning> BiomeFilterWarnings => _biomeFilterWarnings;

    public bool HasMinMaxWarnings => _minMaxWarnings.Count > 0;
    public bool HasPoolWarnings => _poolWarnings.Count > 0;
    public bool HasFactionAffinityWarnings => _factionAffinityWarnings.Count > 0;
    public bool HasBiomeFilterWarnings => _biomeFilterWarnings.Count > 0;

    /// <summary>
    /// Item identity used to join warnings to this row. Mirrors the Web projection
    /// (ZoneContentEditor.razor): handle when present, else <c>#index</c>.
    /// </summary>
    public string KeyForIndex(int index) =>
        string.IsNullOrEmpty(_handleText) ? $"#{index}" : _handleText;

    /// <summary>
    /// Replaces the warning bag and fires PropertyChanged for the bag and both
    /// derived helpers (<see cref="WarningCount"/>, <see cref="HasWarnings"/>).
    /// Internal so only the panel VM populates it.
    /// </summary>
    internal void SetWarnings(IReadOnlyList<EmitWarning> warnings)
    {
        if (ReferenceEquals(_warnings, warnings)) return;
        _warnings = warnings;
        // Precompute per-field projections once so binding hot paths read
        // a cached field instead of re-running Where().ToList() on every
        // PropertyChanged invalidation.
        _minMaxWarnings = WarningsFor(EmitWarning.Codes.MinCountRangeNarrowedToMax);
        _poolWarnings = WarningsFor(EmitWarning.Codes.PoolNonMandatoryDropped);
        _factionAffinityWarnings = WarningsFor(EmitWarning.Codes.FactionAffinityIgnored);
        _biomeFilterWarnings = WarningsFor(EmitWarning.Codes.BiomeFilterIgnored);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Warnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinMaxWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PoolWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FactionAffinityWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BiomeFilterWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMinMaxWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPoolWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFactionAffinityWarnings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasBiomeFilterWarnings)));
    }

    public static ZoneContentItemViewModel FromModel(ZoneContentItem model) => new()
    {
        _sid = model.Sid,
        _handleText = model.Handle ?? "",
        _isGroup = model.IsGroup,
        _minCount = model.MinCount,
        _maxCount = model.MaxCount,
        _pool = model.Pool,
        _isGuarded = model.IsGuarded,
        _nearCastle = model.NearCastle,
        _roadDistance = model.RoadDistance,
        _factionAffinityCsv = JoinCsv(model.FactionAffinity),
        _biomeFilterCsv = JoinCsv(model.BiomeFilter),
        _rulesCsv = ZoneContentRuleCsv.Join(model.Rules),
        _includeListIdsCsv = JoinCsv(model.IncludeListIds),
    };

    public ZoneContentItem ToModel() => new()
    {
        Sid = _sid,
        Handle = string.IsNullOrWhiteSpace(_handleText) ? null : _handleText,
        IsGroup = _isGroup,
        MinCount = _minCount,
        MaxCount = _maxCount,
        Pool = _pool,
        IsGuarded = _isGuarded,
        NearCastle = _nearCastle,
        RoadDistance = _roadDistance,
        FactionAffinity = SplitCsv(_factionAffinityCsv),
        BiomeFilter = SplitCsv(_biomeFilterCsv),
        Rules = ZoneContentRuleCsv.Parse(_rulesCsv),
        IncludeListIds = SplitCsv(_includeListIdsCsv),
    };

    private static List<string> SplitCsv(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? new List<string>()
            : s!.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    private static string JoinCsv(IEnumerable<string> values) => string.Join(", ", values);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
