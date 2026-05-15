using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent;

/// <summary>
/// Centralizes deep-cloning of the zone-content trees on
/// <see cref="GeneratorSettings"/>. <c>SettingsMapper</c> aliases these
/// trees rather than copying (see SettingsMapper.cs:161,271), so any UI
/// that mutates list shape must clone first to avoid editing the
/// in-memory backing instance.
/// </summary>
public static class ZoneContentCloning
{
    public static ZoneContentItem CloneItem(ZoneContentItem source)
    {
        return new ZoneContentItem
        {
            Sid = source.Sid,
            Handle = source.Handle,
            IsGroup = source.IsGroup,
            MinCount = source.MinCount,
            MaxCount = source.MaxCount,
            Pool = source.Pool,
            IsGuarded = source.IsGuarded,
            NearCastle = source.NearCastle,
            RoadDistance = source.RoadDistance,
            FactionAffinity = new List<string>(source.FactionAffinity),
            BiomeFilter = new List<string>(source.BiomeFilter),
            IncludeListIds = new List<string>(source.IncludeListIds),
            Rules = source.Rules.Select(r => new ZoneContentRule
            {
                Type = r.Type,
                Args = new List<string>(r.Args),
                TargetMin = r.TargetMin,
                TargetMax = r.TargetMax,
                Weight = r.Weight,
            }).ToList(),
        };
    }

    public static ZoneContentList CloneList(ZoneContentList source)
    {
        return new ZoneContentList
        {
            Items = source.Items.Select(CloneItem).ToList(),
        };
    }

    public static NeutralZoneContent CloneNeutral(NeutralZoneContent source)
    {
        var clone = new NeutralZoneContent
        {
            Global = CloneList(source.Global),
        };
        foreach (var kvp in source.ByTier)
        {
            clone.ByTier[kvp.Key] = CloneList(kvp.Value);
        }
        foreach (var kvp in source.ByZoneLetter)
        {
            clone.ByZoneLetter[kvp.Key] = CloneList(kvp.Value);
        }
        return clone;
    }

    public static ZoneRoadEndpoint CloneRoadEndpoint(ZoneRoadEndpoint source)
    {
        return new ZoneRoadEndpoint
        {
            Kind = source.Kind,
            Arg = source.Arg,
        };
    }

    public static ZoneRoadDecoration CloneRoadDecoration(ZoneRoadDecoration source)
    {
        return new ZoneRoadDecoration
        {
            Zone = source.Zone,
            RoadType = source.RoadType,
            From = CloneRoadEndpoint(source.From),
            To = CloneRoadEndpoint(source.To),
        };
    }

    public static List<ZoneRoadDecoration> CloneRoadDecorations(List<ZoneRoadDecoration> source)
    {
        return source.Select(CloneRoadDecoration).ToList();
    }

    /// <summary>
    /// Returns a shallow clone of <paramref name="source"/> with the four
    /// zone-content properties replaced by fresh empty instances. Used by
    /// the inspect-defaults UX where the user wants to see what the
    /// generator would emit without their per-zone overrides.
    /// </summary>
    /// <remarks>
    /// Implementation note: <c>SettingsShareCodec</c> only round-trips
    /// <c>SettingsFile</c>, not <c>GeneratorSettings</c>, so we use
    /// <see cref="object.MemberwiseClone"/> via a small helper plus
    /// reassignment of the zone-content slots. The non-zone-content
    /// nested settings (HeroSettings, ZoneCfg, Terrain, etc.) remain
    /// shared with the source — that's fine for the defaults-compare
    /// read-only view, which never mutates them.
    /// </remarks>
    public static GeneratorSettings CloneWithDefaultsBlanked(GeneratorSettings source)
    {
        var clone = ShallowClone(source);
        clone.PlayerZoneContent = new ZoneContentList();
        clone.NeutralZoneContent = new NeutralZoneContent();
        clone.ZoneRoadDecorations = new List<ZoneRoadDecoration>();
        return clone;
    }

    private static GeneratorSettings ShallowClone(GeneratorSettings source)
    {
        // Reflection-based shallow copy; avoids exposing MemberwiseClone.
        var clone = new GeneratorSettings();
        foreach (var prop in typeof(GeneratorSettings).GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            prop.SetValue(clone, prop.GetValue(source));
        }
        return clone;
    }
}
