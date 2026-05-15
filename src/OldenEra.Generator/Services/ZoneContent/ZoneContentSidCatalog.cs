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

        // (resource-suffix, friendly-mine-name) pairs driving both the
        // raw `mine_<resource>` SIDs and the `name_mine_<resource>` anchor
        // SIDs. Order is load-bearing — preserved in catalog enumeration.
        private static readonly (string Resource, string Friendly)[] _mineResources =
        {
            ("gold",      "Gold Mine"),
            ("wood",      "Sawmill"),
            ("ore",       "Ore Pit"),
            ("crystals",  "Crystal Cavern"),
            ("gemstones", "Gem Mine"),
            ("mercury",   "Mercury Lab"),
        };

        private static IEnumerable<ZoneContentSidEntry> BuildSeed()
        {
            // ---- Mandatory anchors (name_* — referenced by mandatory_content
            //      rows in shipped GameData/GeneratorData and example templates) ----
            yield return new("name_mana_well",            "Mana Well (anchor)",                  CategoryMandatory);
            yield return new("name_pandora_box_army",     "Pandora's Box - Army (anchor)",       CategoryMandatory);
            yield return new("name_pandora_box_resources","Pandora's Box - Resources (anchor)",  CategoryMandatory);
            yield return new("name_pandora_box_xp",       "Pandora's Box - XP (anchor)",         CategoryMandatory);
            yield return new("name_alchemy_lab",          "Alchemy Lab (anchor)",                CategoryMandatory);
            yield return new("name_town_gate",            "Town Gate (anchor)",                  CategoryMandatory);

            // ---- Mines (name_* anchors — used by templates as mandatory mines) ----
            // Gold has a base anchor plus 4 numbered duplicates (templates that
            // place multiple gold mines at fixed map positions).
            yield return new("name_mine_gold", "Gold Mine (anchor)", CategoryMines);
            foreach (var n in Enumerable.Range(1, 4))
                yield return new($"name_mine_gold_{n}", $"Gold Mine #{n} (anchor)", CategoryMines);

            // Single-anchor mines for the remaining resources.
            foreach (var (resource, friendly) in _mineResources.Skip(1))
                yield return new($"name_mine_{resource}", $"{friendly} (anchor)", CategoryMines);

            // Biome-themed mine: base anchor plus 3 numbered duplicates.
            yield return new("name_mine_by_biome", "Biome-Themed Mine (anchor)", CategoryMines);
            foreach (var n in Enumerable.Range(1, 3))
                yield return new($"name_mine_by_biome_{n}", $"Biome Mine #{n} (anchor)", CategoryMines);

            // ---- Mines (raw object SIDs — placeable directly via row.Sid) ----
            foreach (var (resource, friendly) in _mineResources)
                yield return new($"mine_{resource}", friendly, CategoryMines);

            // ---- Learning structures (skill / spell / stat trainers) ----
            yield return new("university",                "University",                  CategoryLearning);
            yield return new("college_of_wonder",         "College of Wonder",           CategoryLearning);
            yield return new("pile_of_books",             "Pile of Books",               CategoryLearning);
            yield return new("wise_owl",                  "Wise Owl",                    CategoryLearning);
            yield return new("research_laboratory",       "Research Laboratory",         CategoryLearning);
            yield return new("chimerologist",             "Chimerologist",               CategoryLearning);
            yield return new("mystical_tower",            "Mystical Tower",              CategoryLearning);
            yield return new("orb_observatory",           "Orb Observatory",             CategoryLearning);
            yield return new("petrified_memorial",        "Petrified Memorial",          CategoryLearning);

            // ---- Artifact spots (random-tier rolls + special scroll caches) ----
            yield return new("random_item_common",        "Random Artifact - Common",    CategoryArtifacts);
            yield return new("random_item_rare",          "Random Artifact - Rare",      CategoryArtifacts);
            yield return new("random_item_epic",          "Random Artifact - Epic",      CategoryArtifacts);
            yield return new("random_item_legendary",     "Random Artifact - Legendary", CategoryArtifacts);
            yield return new("mythic_scroll_box",         "Mythic Scroll Box",           CategoryArtifacts);

            // ---- Banks & utopias (creature banks / treasure caches) ----
            yield return new("dragon_utopia",             "Dragon Utopia",               CategoryBanks);
            yield return new("eternal_dragon",            "Eternal Dragon",              CategoryBanks);
            yield return new("monty_hall",                "Monty Hall",                  CategoryBanks);
            yield return new("the_gorge",                 "The Gorge",                   CategoryBanks);
            yield return new("crystal_trail",             "Crystal Trail",               CategoryBanks);
            yield return new("infernal_cirque",           "Infernal Cirque",             CategoryBanks);
            yield return new("twilight_bloom",            "Twilight Bloom",              CategoryBanks);
            yield return new("boreal_call",               "Boreal Call",                 CategoryBanks);
            yield return new("insaras_eye",               "Insara's Eye",                CategoryBanks);
            yield return new("quixs_path",                "Quix's Path",                 CategoryBanks);
            yield return new("unstable_ruins",            "Unstable Ruins",              CategoryBanks);
            yield return new("troglodyte_throne",         "Troglodyte Throne",           CategoryBanks);

            // ---- Pandora & encounters ----
            yield return new("pandora_box",               "Pandora's Box",               CategoryPandora);
            yield return new("prison",                    "Prison",                      CategoryPandora);
            yield return new("mysterious_stone",          "Mysterious Stone",            CategoryPandora);
            yield return new("unforgotten_grave",         "Unforgotten Grave",           CategoryPandora);

            // ---- Shrines & conflux (alignment / faction / mana shrines) ----
            yield return new("sacrificial_shrine",        "Sacrificial Shrine",          CategoryShrines);
            yield return new("fickle_shrine",             "Fickle Shrine",               CategoryShrines);
            yield return new("ritual_pyre",               "Ritual Pyre",                 CategoryShrines);
            yield return new("celestial_sphere",          "Celestial Sphere",            CategoryShrines);
            yield return new("point_of_balance",          "Point of Balance",            CategoryShrines);
            yield return new("tear_of_truth",             "Tear of Truth",               CategoryShrines);
            yield return new("flattering_mirror",         "Flattering Mirror",           CategoryShrines);
            yield return new("tree_of_abundance",         "Tree of Abundance",           CategoryShrines);
            yield return new("mana_well",                 "Mana Well",                   CategoryShrines);

            // ---- Bonus structures (stat / morale / luck / movement) ----
            yield return new("arena",                     "Arena",                       CategoryBonus);
            yield return new("fountain",                  "Fountain of Fortune",         CategoryBonus);
            yield return new("fountain_2",                "Fountain (Variant)",          CategoryBonus);
            yield return new("beer_fountain",             "Beer Fountain",               CategoryBonus);
            yield return new("circus",                    "Circus",                      CategoryBonus);
            yield return new("mirage",                    "Mirage",                      CategoryBonus);
            yield return new("watchtower",                "Watchtower",                  CategoryBonus);
            yield return new("wind_rose",                 "Wind Rose",                   CategoryBonus);
            yield return new("stables",                   "Stables",                     CategoryBonus);
            yield return new("jousting_range",            "Jousting Range",              CategoryBonus);
            yield return new("huntsmans_camp",            "Huntsman's Camp",             CategoryBonus);

            // ---- Hire & recruitment (army-replenish / hero hire) ----
            yield return new("tavern",                    "Tavern",                      CategoryHire);
            yield return new("shady_den",                 "Shady Den",                   CategoryHire);
            // random_hire_1..7 — one slot per creature tier.
            foreach (var tier in Enumerable.Range(1, 7))
                yield return new($"random_hire_{tier}", $"Random Hire (Tier {tier})", CategoryHire);

            // ---- Structures (utility buildings on the map) ----
            yield return new("alchemy_lab",               "Alchemy Lab",                 CategoryStructures);
            yield return new("town_gate",                 "Town Gate",                   CategoryStructures);
            yield return new("market",                    "Market",                      CategoryStructures);
            yield return new("forge",                     "Forge",                       CategoryStructures);
            yield return new("fort",                      "Fort",                        CategoryStructures);

            // ---- Portals (zone connectors) ----
            yield return new("name_portal_gate_center",   "Portal Gate (Center)",        CategoryPortals);
            yield return new("name_portal_gate_side",     "Portal Gate (Side)",          CategoryPortals);
            yield return new("name_portal_gate_spawn",    "Portal Gate (Spawn)",         CategoryPortals);

            // ---- Footholds (template-specific anchors for remote zones) ----
            yield return new("name_remote_foothold", "Remote Foothold", CategoryFootholds);
            foreach (var n in Enumerable.Range(1, 4))
                yield return new($"name_remote_foothold_{n}", $"Remote Foothold #{n}", CategoryFootholds);
            // Doubled-suffix footholds appear in stacked / mirrored templates
            // (e.g. Symmetry, Trinity) where the second instance is suffixed.
            foreach (var n in Enumerable.Range(1, 4))
                yield return new($"name_remote_foothold_{n}{n}", $"Remote Foothold #{n} (mirror)", CategoryFootholds);
            yield return new("remote_foothold", "Remote Foothold (raw SID)", CategoryFootholds);
        }

        private static readonly ZoneContentSidEntry[] _seed = BuildSeed().ToArray();

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
