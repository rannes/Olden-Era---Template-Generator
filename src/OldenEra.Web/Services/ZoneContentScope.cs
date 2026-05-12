using OldenEra.Generator.Models;

namespace OldenEra.Web.Services;

public enum ZoneContentScopeKind
{
    Player,
    NeutralGlobal,
    NeutralPoor,
    NeutralNormal,
    NeutralRich,
    NeutralPerZone,
    RoadDecorations,
}

public readonly record struct ZoneContentScopeKey(ZoneContentScopeKind Kind, string? ZoneLetter = null)
{
    public static ZoneContentScopeKey FromTier(NeutralZoneTier tier) => tier switch
    {
        NeutralZoneTier.Poor => new(ZoneContentScopeKind.NeutralPoor),
        NeutralZoneTier.Normal => new(ZoneContentScopeKind.NeutralNormal),
        NeutralZoneTier.Rich => new(ZoneContentScopeKind.NeutralRich),
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };
}
