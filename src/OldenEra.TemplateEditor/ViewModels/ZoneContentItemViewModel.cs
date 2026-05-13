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
    private IReadOnlyList<EmitWarning> _warnings = Array.Empty<EmitWarning>();

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
    /// Warnings projected for this item. Populated by the panel VM after every
    /// warnings refresh; consumers are read-only. Drives per-row badge display.
    /// </summary>
    public IReadOnlyList<EmitWarning> Warnings => _warnings;

    public int WarningCount => _warnings.Count;

    public bool HasWarnings => _warnings.Count > 0;

    /// <summary>
    /// Filters <see cref="Warnings"/> to entries whose <c>Code</c> equals
    /// <paramref name="code"/>. Mirrors the Web's <c>WarningsFor</c> helper in
    /// <c>ZoneContentItemDetail.razor</c>. Used by per-field badge bindings —
    /// the named properties below (MinMaxWarnings, PoolWarnings, etc.) wrap
    /// the codes that have a UI surface today.
    /// </summary>
    public IReadOnlyList<EmitWarning> WarningsFor(string code) =>
        _warnings.Where(w => w.Code == code).ToList();

    /// <summary>Per-field warning lists driving the WPF badges.</summary>
    public IReadOnlyList<EmitWarning> MinMaxWarnings =>
        WarningsFor(EmitWarning.Codes.MinCountRangeNarrowedToMax);

    public IReadOnlyList<EmitWarning> PoolWarnings =>
        WarningsFor(EmitWarning.Codes.PoolNonMandatoryDropped);

    public IReadOnlyList<EmitWarning> FactionAffinityWarnings =>
        WarningsFor(EmitWarning.Codes.FactionAffinityIgnored);

    public IReadOnlyList<EmitWarning> BiomeFilterWarnings =>
        WarningsFor(EmitWarning.Codes.BiomeFilterIgnored);

    public bool HasMinMaxWarnings => MinMaxWarnings.Count > 0;
    public bool HasPoolWarnings => PoolWarnings.Count > 0;
    public bool HasFactionAffinityWarnings => FactionAffinityWarnings.Count > 0;
    public bool HasBiomeFilterWarnings => BiomeFilterWarnings.Count > 0;

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
