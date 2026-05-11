using System.Collections.Generic;

namespace OldenEra.Generator.Services.ZoneContent
{
    public sealed record ZoneContentSidEntry(string Sid, string FriendlyName, string Category);

    /// <summary>
    /// Curated list of zone-content SIDs surfaced to the picker UI in both the WPF
    /// and Blazor hosts, paired with human-friendly display names and a category tag.
    /// </summary>
    /// <remarks>
    /// This seed is intentionally minimal — currently just the mandatory mana well
    /// and pandora's box variants — because <see cref="GameDataCatalog"/> only
    /// exposes raw SIDs (no friendly names) and <see cref="CommunityCatalog"/>
    /// indexes typed entities (heroes, units, spells) rather than zone-content SIDs.
    /// A follow-up will union this seed with derived entries from those catalogs
    /// once a friendly-name source is wired up.
    /// </remarks>
    public static class ZoneContentSidCatalog
    {
        private static readonly ZoneContentSidEntry[] _seed =
        {
            new("name_mana_well", "Mana Well", "Mandatory"),
            new("name_pandora_box_army", "Pandora's Box (Army)", "Mandatory"),
            new("name_pandora_box_resources", "Pandora's Box (Resources)", "Mandatory"),
            new("name_pandora_box_xp", "Pandora's Box (XP)", "Mandatory"),
        };

        public static IReadOnlyList<ZoneContentSidEntry> All() => _seed;
    }
}
