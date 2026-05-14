using System.Collections.Generic;
using System.Linq;

namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record ZoneContentSidEntry(string Sid, string FriendlyName, string Category);

    /// <summary>
    /// Curated, categorised catalog of zone-content SIDs surfaced to the picker UI
    /// in both the WPF and Blazor hosts. Each entry pairs an SID (the value the
    /// generator emits as <c>row.Sid</c> on a content row, or the
    /// <c>name_*</c> anchor a mandatory-content row binds to) with a
    /// human-friendly display name and a stable category label for grouping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sources for every entry below are restricted to:
    /// <list type="bullet">
    /// <item><description>The shipped <c>src/OldenEra.TemplateEditor/GameData/ExampleTemplates/*.rmg.json</c>
    /// templates — every distinct <c>"name": "name_*"</c> mandatory anchor and every
    /// distinct <c>"sid": "X"</c> value used as a placeable object inside a zone
    /// (excluding GameRules-only <c>add_bonus_*</c> SIDs, which are not zone content).</description></item>
    /// <item><description><see cref="GameDataCatalog"/> coverage in
    /// <c>src/OldenEra.Generator/CommunityData/</c> (read-only; unit catalogs
    /// surface through their own pickers, not this one).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Categories are ordered by <see cref="OrderedCategories"/> for deterministic
    /// grouping in the host UIs. Entries within a category are appended in
    /// source-order; tests assert ordering and uniqueness, and that every SID
    /// appearing in the example templates is reachable through the catalog
    /// (regression net for catalog drift as new templates ship).
    /// </para>
    /// <para>
    /// New SIDs MUST be sourced — do not invent. When refreshing
    /// <c>CommunityData/</c> via the Phase 2 refresh workflow (T-104), re-run
    /// the catalog coverage test to surface any newly-shipped SIDs.
    /// </para>
    /// </remarks>
    public static class ZoneContentSidCatalog
    {
        // Categories. Keep these constants stable — they're persisted in
        // .oetgs/.oetgs.json files indirectly via the picker grouping order.
        public const string CategoryMandatory   = "Mandatory";
        public const string CategoryMines       = "Mines";
        public const string CategoryDwellings   = "Creature dwellings";
        public const string CategoryLearning    = "Learning structures";
        public const string CategoryArtifacts   = "Artifact spots";
        public const string CategoryBanks       = "Banks & utopias";
        public const string CategoryPandora     = "Pandora & encounters";
        public const string CategoryShrines     = "Shrines & conflux";
        public const string CategoryBonus       = "Bonus structures";
        public const string CategoryHire        = "Hire & recruitment";
        public const string CategoryStructures  = "Structures";
        public const string CategoryPortals     = "Portals";
        public const string CategoryFootholds   = "Footholds";
        public const string CategoryMisc        = "Misc";

        /// <summary>
        /// Ordered list of categories used by the host pickers when grouping
        /// entries. Adding a new category? Append it here in display order.
        /// </summary>
        public static readonly IReadOnlyList<string> OrderedCategories = new[]
        {
            CategoryMandatory,
            CategoryMines,
            CategoryDwellings,
            CategoryLearning,
            CategoryArtifacts,
            CategoryBanks,
            CategoryPandora,
            CategoryShrines,
            CategoryBonus,
            CategoryHire,
            CategoryStructures,
            CategoryPortals,
            CategoryFootholds,
            CategoryMisc,
        };

        // Catalog entries. Sourced exclusively from ExampleTemplates/*.rmg.json
        // (`name` mandatory-anchors, `sid` placeable objects).
        // Any change here must keep CatalogCoversExampleTemplates_unit_test green.
        private static readonly ZoneContentSidEntry[] _seed =
        {
            // ---- Mandatory anchors (name_* — referenced by mandatory_content
            //      rows in shipped GameData/GeneratorData and example templates) ----
            new("name_mana_well",            "Mana Well (anchor)",                  CategoryMandatory),
            new("name_pandora_box_army",     "Pandora's Box - Army (anchor)",       CategoryMandatory),
            new("name_pandora_box_resources","Pandora's Box - Resources (anchor)",  CategoryMandatory),
            new("name_pandora_box_xp",       "Pandora's Box - XP (anchor)",         CategoryMandatory),
            new("name_alchemy_lab",          "Alchemy Lab (anchor)",                CategoryMandatory),
            new("name_town_gate",            "Town Gate (anchor)",                  CategoryMandatory),

            // ---- Mines (name_* anchors — used by templates as mandatory mines) ----
            new("name_mine_gold",            "Gold Mine (anchor)",          CategoryMines),
            new("name_mine_gold_1",          "Gold Mine #1 (anchor)",       CategoryMines),
            new("name_mine_gold_2",          "Gold Mine #2 (anchor)",       CategoryMines),
            new("name_mine_gold_3",          "Gold Mine #3 (anchor)",       CategoryMines),
            new("name_mine_gold_4",          "Gold Mine #4 (anchor)",       CategoryMines),
            new("name_mine_wood",            "Sawmill (anchor)",            CategoryMines),
            new("name_mine_ore",             "Ore Pit (anchor)",            CategoryMines),
            new("name_mine_crystals",        "Crystal Cavern (anchor)",     CategoryMines),
            new("name_mine_gemstones",       "Gem Mine (anchor)",           CategoryMines),
            new("name_mine_mercury",         "Mercury Lab (anchor)",        CategoryMines),
            new("name_mine_by_biome",        "Biome-Themed Mine (anchor)",  CategoryMines),
            new("name_mine_by_biome_1",      "Biome Mine #1 (anchor)",      CategoryMines),
            new("name_mine_by_biome_2",      "Biome Mine #2 (anchor)",      CategoryMines),
            new("name_mine_by_biome_3",      "Biome Mine #3 (anchor)",      CategoryMines),

            // ---- Mines (raw object SIDs — placeable directly via row.Sid) ----
            new("mine_gold",                 "Gold Mine",                   CategoryMines),
            new("mine_wood",                 "Sawmill",                     CategoryMines),
            new("mine_ore",                  "Ore Pit",                     CategoryMines),
            new("mine_crystals",             "Crystal Cavern",              CategoryMines),
            new("mine_gemstones",            "Gem Mine",                    CategoryMines),
            new("mine_mercury",              "Mercury Lab",                 CategoryMines),

            // ---- Learning structures (skill / spell / stat trainers) ----
            new("university",                "University",                  CategoryLearning),
            new("college_of_wonder",         "College of Wonder",           CategoryLearning),
            new("pile_of_books",             "Pile of Books",               CategoryLearning),
            new("wise_owl",                  "Wise Owl",                    CategoryLearning),
            new("research_laboratory",       "Research Laboratory",         CategoryLearning),
            new("chimerologist",             "Chimerologist",               CategoryLearning),
            new("mystical_tower",            "Mystical Tower",              CategoryLearning),
            new("orb_observatory",           "Orb Observatory",             CategoryLearning),
            new("petrified_memorial",        "Petrified Memorial",          CategoryLearning),

            // ---- Artifact spots (random-tier rolls + special scroll caches) ----
            new("random_item_common",        "Random Artifact - Common",    CategoryArtifacts),
            new("random_item_rare",          "Random Artifact - Rare",      CategoryArtifacts),
            new("random_item_epic",          "Random Artifact - Epic",      CategoryArtifacts),
            new("random_item_legendary",     "Random Artifact - Legendary", CategoryArtifacts),
            new("mythic_scroll_box",         "Mythic Scroll Box",           CategoryArtifacts),

            // ---- Banks & utopias (creature banks / treasure caches) ----
            new("dragon_utopia",             "Dragon Utopia",               CategoryBanks),
            new("eternal_dragon",            "Eternal Dragon",              CategoryBanks),
            new("monty_hall",                "Monty Hall",                  CategoryBanks),
            new("the_gorge",                 "The Gorge",                   CategoryBanks),
            new("crystal_trail",             "Crystal Trail",               CategoryBanks),
            new("infernal_cirque",           "Infernal Cirque",             CategoryBanks),
            new("twilight_bloom",            "Twilight Bloom",              CategoryBanks),
            new("boreal_call",               "Boreal Call",                 CategoryBanks),
            new("insaras_eye",               "Insara's Eye",                CategoryBanks),
            new("quixs_path",                "Quix's Path",                 CategoryBanks),
            new("unstable_ruins",            "Unstable Ruins",              CategoryBanks),
            new("troglodyte_throne",         "Troglodyte Throne",           CategoryBanks),

            // ---- Pandora & encounters ----
            new("pandora_box",               "Pandora's Box",               CategoryPandora),
            new("prison",                    "Prison",                      CategoryPandora),
            new("mysterious_stone",          "Mysterious Stone",            CategoryPandora),
            new("unforgotten_grave",         "Unforgotten Grave",           CategoryPandora),

            // ---- Shrines & conflux (alignment / faction / mana shrines) ----
            new("sacrificial_shrine",        "Sacrificial Shrine",          CategoryShrines),
            new("fickle_shrine",             "Fickle Shrine",               CategoryShrines),
            new("ritual_pyre",               "Ritual Pyre",                 CategoryShrines),
            new("celestial_sphere",          "Celestial Sphere",            CategoryShrines),
            new("point_of_balance",          "Point of Balance",            CategoryShrines),
            new("tear_of_truth",             "Tear of Truth",               CategoryShrines),
            new("flattering_mirror",         "Flattering Mirror",           CategoryShrines),
            new("tree_of_abundance",         "Tree of Abundance",           CategoryShrines),
            new("mana_well",                 "Mana Well",                   CategoryShrines),

            // ---- Bonus structures (stat / morale / luck / movement) ----
            new("arena",                     "Arena",                       CategoryBonus),
            new("fountain",                  "Fountain of Fortune",         CategoryBonus),
            new("fountain_2",                "Fountain (Variant)",          CategoryBonus),
            new("beer_fountain",             "Beer Fountain",               CategoryBonus),
            new("circus",                    "Circus",                      CategoryBonus),
            new("mirage",                    "Mirage",                      CategoryBonus),
            new("watchtower",                "Watchtower",                  CategoryBonus),
            new("wind_rose",                 "Wind Rose",                   CategoryBonus),
            new("stables",                   "Stables",                     CategoryBonus),
            new("jousting_range",            "Jousting Range",              CategoryBonus),
            new("huntsmans_camp",            "Huntsman's Camp",             CategoryBonus),

            // ---- Hire & recruitment (army-replenish / hero hire) ----
            new("tavern",                    "Tavern",                      CategoryHire),
            new("shady_den",                 "Shady Den",                   CategoryHire),
            new("random_hire_1",             "Random Hire (Tier 1)",        CategoryHire),
            new("random_hire_2",             "Random Hire (Tier 2)",        CategoryHire),
            new("random_hire_3",             "Random Hire (Tier 3)",        CategoryHire),
            new("random_hire_4",             "Random Hire (Tier 4)",        CategoryHire),
            new("random_hire_5",             "Random Hire (Tier 5)",        CategoryHire),
            new("random_hire_6",             "Random Hire (Tier 6)",        CategoryHire),
            new("random_hire_7",             "Random Hire (Tier 7)",        CategoryHire),

            // ---- Structures (utility buildings on the map) ----
            new("alchemy_lab",               "Alchemy Lab",                 CategoryStructures),
            new("town_gate",                 "Town Gate",                   CategoryStructures),
            new("market",                    "Market",                      CategoryStructures),
            new("forge",                     "Forge",                       CategoryStructures),
            new("fort",                      "Fort",                        CategoryStructures),

            // ---- Portals (zone connectors) ----
            new("name_portal_gate_center",   "Portal Gate (Center)",        CategoryPortals),
            new("name_portal_gate_side",     "Portal Gate (Side)",          CategoryPortals),
            new("name_portal_gate_spawn",    "Portal Gate (Spawn)",         CategoryPortals),

            // ---- Footholds (template-specific anchors for remote zones) ----
            new("name_remote_foothold",      "Remote Foothold",             CategoryFootholds),
            new("name_remote_foothold_1",    "Remote Foothold #1",          CategoryFootholds),
            new("name_remote_foothold_2",    "Remote Foothold #2",          CategoryFootholds),
            new("name_remote_foothold_3",    "Remote Foothold #3",          CategoryFootholds),
            new("name_remote_foothold_4",    "Remote Foothold #4",          CategoryFootholds),
            // Doubled-suffix footholds appear in stacked / mirrored templates
            // (e.g. Symmetry, Trinity) where the second instance is suffixed.
            new("name_remote_foothold_11",   "Remote Foothold #1 (mirror)", CategoryFootholds),
            new("name_remote_foothold_22",   "Remote Foothold #2 (mirror)", CategoryFootholds),
            new("name_remote_foothold_33",   "Remote Foothold #3 (mirror)", CategoryFootholds),
            new("name_remote_foothold_44",   "Remote Foothold #4 (mirror)", CategoryFootholds),
            new("remote_foothold",           "Remote Foothold (raw SID)",   CategoryFootholds),
        };

        /// <summary>
        /// Flat, source-ordered list of every catalog entry. Stable enumeration
        /// for free-text autocomplete in the host pickers.
        /// </summary>
        public static IReadOnlyList<ZoneContentSidEntry> All() => _seed;

        /// <summary>
        /// Returns entries grouped by <see cref="ZoneContentSidEntry.Category"/>,
        /// with categories ordered by <see cref="OrderedCategories"/> and entries
        /// inside each category preserving source order. Hosts use this for
        /// grouped picker UI (WPF CollectionViewSource group descriptions, Web
        /// optgroup/datalist labelling).
        /// </summary>
        public static IReadOnlyList<IGrouping<string, ZoneContentSidEntry>> Grouped()
        {
            var order = OrderedCategories
                .Select((c, i) => (c, i))
                .ToDictionary(t => t.c, t => t.i);
            return _seed
                .GroupBy(e => e.Category)
                .OrderBy(g => order.TryGetValue(g.Key, out var i) ? i : int.MaxValue)
                .ToList();
        }
    }
}
