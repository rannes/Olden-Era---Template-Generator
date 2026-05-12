using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    /// <summary>
    /// Curated, built-in <see cref="ZoneContentItem"/> presets that the host UI
    /// surfaces through an "Add preset…" affordance, so users do not have to
    /// configure every knob from scratch when adding common rows. Each preset
    /// is hand-picked and guaranteed to pass <see cref="ZoneContentItemValidator"/>.
    /// </summary>
    public sealed record ZoneContentPreset(string Name, ZoneContentItem Item);

    /// <summary>
    /// Static catalog of curated <see cref="ZoneContentPreset"/> entries. The host
    /// UI offers these via an "Add preset…" affordance to seed the customizable
    /// zone content list with sensible, validated defaults.
    /// </summary>
    public static class ZoneContentPresets
    {
        public static IReadOnlyList<ZoneContentPreset> All() => new ZoneContentPreset[]
        {
            new("Mana Well x1 (guarded)", new ZoneContentItem
            {
                Sid = "name_mana_well", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true,
            }),
            new("Pandora Army x1 (guarded, near castle)", new ZoneContentItem
            {
                Sid = "name_pandora_box_army", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true, NearCastle = true,
            }),
            new("Pandora Resources x1", new ZoneContentItem
            {
                Sid = "name_pandora_box_resources", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Pandora XP x1", new ZoneContentItem
            {
                Sid = "name_pandora_box_xp", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
        };
    }
}
