using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent;

public sealed record ZoneContentWarning(
    ZoneContentScopeKey Scope,
    string? Handle,
    int ItemIndex,
    EmitWarning Warning);

public static class ZoneContentWarningProjection
{
    public static IReadOnlyList<ZoneContentWarning> Project(GeneratorSettings settings)
    {
        var result = new List<ZoneContentWarning>();

        Inspect(settings.PlayerZoneContent, new ZoneContentScopeKey(ZoneContentScopeKind.Player), zoneName: "Player", result);

        Inspect(settings.NeutralZoneContent.Global, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralGlobal), zoneName: "Neutral", result);

        foreach (var (tier, list) in settings.NeutralZoneContent.ByTier)
            Inspect(list, ZoneContentScopeKey.FromTier(tier), zoneName: $"Neutral.{tier}", result);

        foreach (var (letter, list) in settings.NeutralZoneContent.ByZoneLetter)
            Inspect(list, new ZoneContentScopeKey(ZoneContentScopeKind.NeutralPerZone, letter), zoneName: letter, result);

        return result;
    }

    private static void Inspect(
        ZoneContentList list,
        ZoneContentScopeKey scope,
        string? zoneName,
        List<ZoneContentWarning> result)
    {
        for (var i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            foreach (var w in ZoneContentEmitWarnings.Inspect(item, zoneName))
                result.Add(new ZoneContentWarning(scope, item.Handle, i, w));
        }
    }
}
