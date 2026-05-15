using System.Collections.Generic;
using System.Linq;

namespace OldenEra.Generator.Services.ZoneContent
{
    /// <summary>
    /// One entry in <see cref="ContentListCatalog"/>: a content-list ID
    /// (the value emitted in <c>ContentItem.includeLists</c>), a short
    /// display label, and a stable category for picker grouping.
    /// </summary>
    public sealed record ContentListEntry(string Id, string Display, string Category);

    /// <summary>
    /// Curated catalog of <c>includeLists</c> IDs surfaced to the picker UI in
    /// both the WPF and Blazor hosts (T-605). Each entry pairs a content-list
    /// ID with a human-friendly display label and a category for grouping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IDs were mined from every <c>ContentItem.includeLists</c> array in the
    /// shipped <c>src/OldenEra.TemplateEditor/GameData/ExampleTemplates/*.rmg.json</c>
    /// templates as of T-605 (44 distinct IDs across ~30 templates). The
    /// coverage test in <c>ContentListCatalogTests</c> re-runs this scan and
    /// fails if a shipped template references an ID this catalog cannot
    /// surface to the user — keeping the catalog from drifting as new
    /// templates ship.
    /// </para>
    /// <para>
    /// Categories are intentionally semantic ("Resource banks", "Units banks",
    /// "Random hires", …) rather than ID-prefix-based: shipped templates use
    /// both <c>basic_content_list_*</c>, <c>content_list_*</c>, and
    /// <c>template_pool_*</c> prefixes for the same conceptual group, and the
    /// picker should hide that prefix detail. New IDs that don't fit any
    /// existing category should land in <see cref="CategoryOther"/> rather
    /// than spawning a one-off bucket; the coverage test treats "other" as a
    /// valid catch-all.
    /// </para>
    /// <para>
    /// Stability contract: IDs round-trip verbatim from shipped templates
    /// through the WPF/Web pickers and back into emitted JSON via
    /// <see cref="ZoneContentEmitter"/>. Renaming a category is a UI-only
    /// change; renaming an ID would break round-trip.
    /// </para>
    /// </remarks>
    public static class ContentListCatalog
    {
        public const string CategoryResourceBanks = "Resource banks";
        public const string CategoryUnitsBanks    = "Units banks";
        public const string CategoryRandomHires   = "Random hires";
        public const string CategoryHeroLearning  = "Hero stats / magic";
        public const string CategoryPickups       = "Pickups & pandoras";
        public const string CategoryMines         = "Mines";
        public const string CategoryOther         = "Other";

        /// <summary>
        /// Display order for category sections in the picker. New categories
        /// must be appended here to surface in the host UIs.
        /// </summary>
        public static readonly IReadOnlyList<string> OrderedCategories = new[]
        {
            CategoryResourceBanks,
            CategoryUnitsBanks,
            CategoryRandomHires,
            CategoryHeroLearning,
            CategoryPickups,
            CategoryMines,
            CategoryOther,
        };

        private static IEnumerable<ContentListEntry> BuildSeed()
        {
            // ---- Resource banks ----
            yield return new("basic_content_list_building_guarded_resource_banks_tier_1",
                "Resource banks · tier 1 (basic)", CategoryResourceBanks);
            yield return new("basic_content_list_building_guarded_resource_banks_tier_2",
                "Resource banks · tier 2 (basic)", CategoryResourceBanks);
            yield return new("basic_content_list_building_guarded_resource_banks_tier_3",
                "Resource banks · tier 3 (basic)", CategoryResourceBanks);
            yield return new("content_list_building_epic_guarded_resource_banks",
                "Resource banks · epic", CategoryResourceBanks);
            // template_pool_* — per-template overrides for tier-3 resource banks.
            yield return new("template_pool_blitz_guarded_resource_banks_tier_3_base",
                "Resource banks · Blitz tier-3 (base)", CategoryResourceBanks);
            yield return new("template_pool_blitz_guarded_resource_banks_tier_3_pro",
                "Resource banks · Blitz tier-3 (pro)", CategoryResourceBanks);
            yield return new("template_pool_chosen_one_guarded_resource_banks_tier_3_base",
                "Resource banks · Chosen One tier-3 (base)", CategoryResourceBanks);
            yield return new("template_pool_jebus_cross_guarded_resource_banks_tier_3_base",
                "Resource banks · Jebus Cross tier-3 (base)", CategoryResourceBanks);
            yield return new("template_pool_jebus_cross_guarded_resource_banks_tier_3_pro",
                "Resource banks · Jebus Cross tier-3 (pro)", CategoryResourceBanks);
            yield return new("template_pool_massacre_guarded_resource_banks_tier_3_base",
                "Resource banks · Massacre tier-3 (base)", CategoryResourceBanks);
            yield return new("template_pool_shamrock_guarded_resource_banks_tier_3_base",
                "Resource banks · Shamrock tier-3 (base)", CategoryResourceBanks);

            // ---- Units banks (creature banks gated by biome / template) ----
            yield return new("basic_content_list_building_guarded_units_banks",
                "Units banks (basic)", CategoryUnitsBanks);
            yield return new("basic_content_list_building_guarded_units_banks_no_biome_restriction",
                "Units banks · no biome restriction", CategoryUnitsBanks);
            yield return new("basic_content_list_building_guarded_units_banks_only_biome_restriction",
                "Units banks · biome-restricted only", CategoryUnitsBanks);
            yield return new("content_list_building_uncommon_guarded_units_banks",
                "Units banks · uncommon", CategoryUnitsBanks);
            yield return new("content_list_building_template_sprint_uncommon_guarded_units_banks",
                "Units banks · Sprint uncommon", CategoryUnitsBanks);

            // ---- Random hires (recruit pools, all tiers) ----
            yield return new("basic_content_list_building_random_hires",
                "Random hires (basic, all tiers)", CategoryRandomHires);
            for (int tier = 1; tier <= 7; tier++)
            {
                yield return new($"basic_content_list_building_random_hires_tier_{tier}",
                    $"Random hires · tier {tier} (basic)", CategoryRandomHires);
            }
            yield return new("content_list_building_random_hires",
                "Random hires", CategoryRandomHires);
            yield return new("content_list_building_random_hires_high_tier",
                "Random hires · high tier", CategoryRandomHires);
            yield return new("content_list_building_random_hires_low_tier",
                "Random hires · low tier", CategoryRandomHires);
            // Harmony per-zone hire pools (Center / Side / Spawn).
            yield return new("template_pool_harmony_building_random_hires_center",
                "Random hires · Harmony center", CategoryRandomHires);
            yield return new("template_pool_harmony_building_random_hires_side",
                "Random hires · Harmony side", CategoryRandomHires);
            yield return new("template_pool_harmony_building_random_hires_spawn",
                "Random hires · Harmony spawn", CategoryRandomHires);

            // ---- Hero stats / magic (learning-style buildings) ----
            yield return new("basic_content_list_building_hero_buff_tier_1",
                "Hero buff · tier 1", CategoryHeroLearning);
            yield return new("basic_content_list_building_hero_stats_and_skills_tier_2",
                "Hero stats & skills · tier 2", CategoryHeroLearning);
            yield return new("basic_content_list_building_hero_stats_and_skills_tier_3",
                "Hero stats & skills · tier 3", CategoryHeroLearning);
            yield return new("basic_content_list_building_magic_tier_2",
                "Magic structures · tier 2", CategoryHeroLearning);
            yield return new("content_list_building_uncommon_hero_stats",
                "Hero stats · uncommon", CategoryHeroLearning);

            // ---- Pickups & pandoras ----
            yield return new("basic_content_list_pickup_mythic_scroll_box",
                "Mythic scroll box", CategoryPickups);
            yield return new("basic_content_list_pickup_pandora_box_units",
                "Pandora box · units", CategoryPickups);
            yield return new("basic_content_list_pickup_random_items",
                "Random items pickup", CategoryPickups);
            for (int tier = 1; tier <= 5; tier++)
            {
                yield return new($"content_list_pickup_scroll_box_tier_{tier}",
                    $"Scroll box · tier {tier}", CategoryPickups);
            }

            // ---- Mines ----
            yield return new("basic_content_list_rare_mines_by_biome",
                "Rare mines · by biome", CategoryMines);
        }

        private static readonly ContentListEntry[] _seed = BuildSeed().ToArray();

        /// <summary>Flat, source-ordered list of catalog entries.</summary>
        public static IReadOnlyList<ContentListEntry> All() => _seed;

        /// <summary>
        /// Entries grouped by <see cref="ContentListEntry.Category"/>, with
        /// categories ordered per <see cref="OrderedCategories"/> and entries
        /// inside a category preserving source order.
        /// </summary>
        public static IReadOnlyList<IGrouping<string, ContentListEntry>> Grouped()
        {
            var order = OrderedCategories
                .Select((c, i) => (c, i))
                .ToDictionary(t => t.c, t => t.i);
            return _seed
                .GroupBy(e => e.Category)
                .OrderBy(g => order.TryGetValue(g.Key, out var i) ? i : int.MaxValue)
                .ToList();
        }

        /// <summary>True if <paramref name="id"/> is a known catalog entry.</summary>
        public static bool Contains(string id) =>
            _seed.Any(e => string.Equals(e.Id, id, System.StringComparison.Ordinal));
    }
}
