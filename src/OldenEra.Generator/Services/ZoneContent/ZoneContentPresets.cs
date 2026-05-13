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
    /// <param name="Name">Display label for the preset in the picker.</param>
    /// <param name="Category">Grouping label used by host UIs to organize the
    /// picker (Web <c>&lt;optgroup&gt;</c>, WPF <c>CollectionViewSource</c> grouping).
    /// Use one of the constants on <see cref="ZoneContentPresets"/>.</param>
    /// <param name="Item">The preset row contents.</param>
    public sealed record ZoneContentPreset(string Name, string Category, ZoneContentItem Item);

    /// <summary>
    /// Static catalog of curated <see cref="ZoneContentPreset"/> entries. The host
    /// UI offers these via an "Add preset…" affordance to seed the customizable
    /// zone content list with sensible, validated defaults.
    /// </summary>
    public static class ZoneContentPresets
    {
        public const string CategoryMandatory = "Mandatory";

        public static IReadOnlyList<ZoneContentPreset> All() => new ZoneContentPreset[]
        {
            new("Mana Well x1 (guarded)", CategoryMandatory, new ZoneContentItem
            {
                Sid = "name_mana_well", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true,
            }),
            new("Pandora Army x1 (guarded, near castle)", CategoryMandatory, new ZoneContentItem
            {
                Sid = "name_pandora_box_army", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true, NearCastle = true,
            }),
            new("Pandora Resources x1", CategoryMandatory, new ZoneContentItem
            {
                Sid = "name_pandora_box_resources", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Pandora XP x1", CategoryMandatory, new ZoneContentItem
            {
                Sid = "name_pandora_box_xp", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
        };
    }
}
