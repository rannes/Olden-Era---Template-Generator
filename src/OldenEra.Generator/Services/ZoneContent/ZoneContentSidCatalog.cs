using System.Collections.Generic;

namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record ZoneContentSidEntry(string Sid, string FriendlyName, string Category);

    /// <summary>
    /// Curated list of zone-content SIDs surfaced to the picker UI in both the WPF
    /// and Blazor hosts, paired with human-friendly display names and a category tag.
    /// </summary>
    /// <remarks>
    /// Entries are hand-authored from real SIDs that appear in the shipped
    /// <c>GameData/GeneratorData</c> tree (zone layouts and mandatory-content
    /// references). <see cref="GameDataCatalog"/> only exposes raw SIDs (no
    /// friendly names) and <see cref="CommunityCatalog"/> indexes typed entities
    /// (heroes, units, spells), so this seed acts as the friendly-name source
    /// for the host pickers.
    /// </remarks>
    public static class ZoneContentSidCatalog
    {
        public const string CategoryMandatory = "Mandatory";
        public const string CategoryResources = "Resources";
        public const string CategoryMines = "Mines";
        public const string CategoryStructures = "Structures";
        public const string CategoryPortals = "Portals";
        public const string CategoryFootholds = "Footholds";

        private static readonly ZoneContentSidEntry[] _seed =
        {
            // Mandatory pickups / encounters
            new("name_mana_well", "Mana Well", CategoryMandatory),
            new("name_pandora_box_army", "Pandora's Box (Army)", CategoryMandatory),
            new("name_pandora_box_resources", "Pandora's Box (Resources)", CategoryMandatory),
            new("name_pandora_box_xp", "Pandora's Box (XP)", CategoryMandatory),

            // Structures
            new("name_alchemy_lab", "Alchemy Lab", CategoryStructures),
            new("name_town_gate", "Town Gate", CategoryStructures),

            // Mines (resource generators)
            new("name_mine_gold", "Gold Mine", CategoryMines),
            new("name_mine_wood", "Sawmill (Wood)", CategoryMines),
            new("name_mine_ore", "Ore Pit", CategoryMines),
            new("name_mine_crystals", "Crystal Cavern", CategoryMines),
            new("name_mine_gemstones", "Gem Mine", CategoryMines),
            new("name_mine_mercury", "Mercury Lab", CategoryMines),
            new("name_mine_by_biome", "Biome-Themed Mine", CategoryMines),

            // Portals (zone connectors)
            new("name_portal_gate_center", "Portal Gate (Center)", CategoryPortals),
            new("name_portal_gate_side", "Portal Gate (Side)", CategoryPortals),
            new("name_portal_gate_spawn", "Portal Gate (Spawn)", CategoryPortals),

            // Remote footholds (template-specific anchors)
            new("name_remote_foothold", "Remote Foothold", CategoryFootholds),
            new("name_remote_foothold_1", "Remote Foothold #1", CategoryFootholds),
            new("name_remote_foothold_2", "Remote Foothold #2", CategoryFootholds),
            new("name_remote_foothold_3", "Remote Foothold #3", CategoryFootholds),
            new("name_remote_foothold_4", "Remote Foothold #4", CategoryFootholds),
        };

        public static IReadOnlyList<ZoneContentSidEntry> All() => _seed;
    }
}
