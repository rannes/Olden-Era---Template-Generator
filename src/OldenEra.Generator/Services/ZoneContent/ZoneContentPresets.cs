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
        public const string CategoryMines = "Mines";
        public const string CategoryStructures = "Structures";
        public const string CategoryPortals = "Portals";
        public const string CategoryFootholds = "Footholds";

        public static IReadOnlyList<ZoneContentPreset> All() => new ZoneContentPreset[]
        {
            // ---------- Mandatory ----------
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
            new("Mana Well x2 (guarded)", CategoryMandatory, new ZoneContentItem
            {
                Sid = "name_mana_well", MinCount = 2, MaxCount = 2,
                Pool = ZoneContentPool.Mandatory, IsGuarded = true,
            }),

            // ---------- Mines ----------
            new("Gold Mine x1 (guarded)", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_gold", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Resources, IsGuarded = true,
            }),
            new("Gold Mine x1-2 (near castle)", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_gold", MinCount = 1, MaxCount = 2,
                Pool = ZoneContentPool.Resources, NearCastle = true,
            }),
            new("Sawmill x1-2", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_wood", MinCount = 1, MaxCount = 2,
                Pool = ZoneContentPool.Resources,
            }),
            new("Ore Pit x1-2", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_ore", MinCount = 1, MaxCount = 2,
                Pool = ZoneContentPool.Resources,
            }),
            new("Crystal Cavern x1 (guarded)", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_crystals", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Resources, IsGuarded = true,
            }),
            new("Gem Mine x1 (guarded)", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_gemstones", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Resources, IsGuarded = true,
            }),
            new("Mercury Lab x1 (guarded)", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_mercury", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Resources, IsGuarded = true,
            }),
            new("Biome Mine x1-2", CategoryMines, new ZoneContentItem
            {
                Sid = "name_mine_by_biome", MinCount = 1, MaxCount = 2,
                Pool = ZoneContentPool.Resources, IsGuarded = true,
            }),

            // ---------- Structures ----------
            new("Alchemy Lab x1 (guarded)", CategoryStructures, new ZoneContentItem
            {
                Sid = "name_alchemy_lab", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Guarded, IsGuarded = true,
            }),
            new("Town Gate x1 (mandatory, near castle)", CategoryStructures, new ZoneContentItem
            {
                Sid = "name_town_gate", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, NearCastle = true,
            }),

            // ---------- Portals ----------
            new("Portal Gate (Center) x1", CategoryPortals, new ZoneContentItem
            {
                Sid = "name_portal_gate_center", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Portal Gate (Side) x1-2", CategoryPortals, new ZoneContentItem
            {
                Sid = "name_portal_gate_side", MinCount = 1, MaxCount = 2,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Portal Gate (Spawn) x1 (near castle)", CategoryPortals, new ZoneContentItem
            {
                Sid = "name_portal_gate_spawn", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory, NearCastle = true,
            }),

            // ---------- Footholds ----------
            new("Remote Foothold x1", CategoryFootholds, new ZoneContentItem
            {
                Sid = "name_remote_foothold", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Remote Foothold #1", CategoryFootholds, new ZoneContentItem
            {
                Sid = "name_remote_foothold_1", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Remote Foothold #2", CategoryFootholds, new ZoneContentItem
            {
                Sid = "name_remote_foothold_2", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Remote Foothold #3", CategoryFootholds, new ZoneContentItem
            {
                Sid = "name_remote_foothold_3", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
            new("Remote Foothold #4", CategoryFootholds, new ZoneContentItem
            {
                Sid = "name_remote_foothold_4", MinCount = 1, MaxCount = 1,
                Pool = ZoneContentPool.Mandatory,
            }),
        };
    }
}
