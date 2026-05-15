using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-703 — content-pool sanity warnings. Each rule fires exactly once per
/// fixture, presets must remain warning-free, and an empty content
/// configuration must not surface any of the new warnings.
/// </summary>
public class SettingsValidatorContentPoolTests
{
    private static GeneratorSettings ValidBaseline() => new()
    {
        TemplateName = "My Template",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Default,
        ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 4 },
    };

    /// <summary>
    /// Adds one item to <see cref="GeneratorSettings.PlayerZoneContent"/> so
    /// the content-pool sweep is engaged. Callers append further items to
    /// produce the rule-specific fixture.
    /// </summary>
    private static GeneratorSettings WithSeededPlayerContent(string seedSid)
    {
        var s = ValidBaseline();
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = seedSid,
            Pool = ZoneContentPool.Mandatory,
        });
        return s;
    }

    private static GeneratorSettings FullCoverageFixture()
    {
        var s = ValidBaseline();
        // Hits every category the validator inspects, so no T-703 warning fires.
        var items = new List<ZoneContentItem>
        {
            new() { Sid = "random_hire_7" },                 // tier-7 hire
            new() { Sid = "sacrificial_shrine" },             // shrine
            new() { Sid = "dragon_utopia" },                  // creature bank
            new() { Sid = "mine_gold" },                      // mine
            new()
            {
                Sid = "anchor_resource_banks_filler",
                IncludeListIds =
                {
                    "basic_content_list_building_guarded_resource_banks_tier_1",
                },
            },
        };
        foreach (var item in items)
            s.PlayerZoneContent.Items.Add(item);
        return s;
    }

    [Fact]
    public void Baseline_NoContentLists_NoContentPoolWarnings()
    {
        var result = SettingsValidator.Validate(ValidBaseline());
        var contentPoolWarnings = result.Issues
            .Where(i => i.FieldKey == ValidationFieldKeys.ZoneContentPool)
            .ToList();
        Assert.Empty(contentPoolWarnings);
    }

    [Fact]
    public void FullCoverage_NoContentPoolWarnings()
    {
        var result = SettingsValidator.Validate(FullCoverageFixture());
        var contentPoolWarnings = result.Issues
            .Where(i => i.FieldKey == ValidationFieldKeys.ZoneContentPool)
            .ToList();
        Assert.Empty(contentPoolWarnings);
    }

    // ── Five canonical misconfig fixtures ──────────────────────────────

    [Fact]
    public void Fixture_NoTier7Dwellings_SurfacesWarning()
    {
        // User configured low-tier hires only; tier 7 is unreachable.
        var s = WithSeededPlayerContent("random_hire_1");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "random_hire_2" });
        // Cover other rules so only the tier-7 warning fires.
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "dragon_utopia" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "mine_gold" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues, i =>
            i.FieldKey == ValidationFieldKeys.ZoneContentPool
            && i.Code == ValidationIssueCodes.ContentPoolNoTier7Dwellings);
    }

    [Fact]
    public void Fixture_NoShrines_SurfacesWarning()
    {
        var s = WithSeededPlayerContent("random_hire_7");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "dragon_utopia" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "mine_gold" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues, i =>
            i.FieldKey == ValidationFieldKeys.ZoneContentPool
            && i.Code == ValidationIssueCodes.ContentPoolNoShrines);
    }

    [Fact]
    public void Fixture_NoCreatureBanks_SurfacesWarning()
    {
        var s = WithSeededPlayerContent("random_hire_7");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "mine_gold" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues, i =>
            i.FieldKey == ValidationFieldKeys.ZoneContentPool
            && i.Code == ValidationIssueCodes.ContentPoolNoCreatureBanks);
    }

    [Fact]
    public void Fixture_NoResourceBanks_SurfacesWarning()
    {
        var s = WithSeededPlayerContent("random_hire_7");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "dragon_utopia" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "mine_gold" });

        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues, i =>
            i.FieldKey == ValidationFieldKeys.ZoneContentPool
            && i.Code == ValidationIssueCodes.ContentPoolNoResourceBanks);
    }

    [Fact]
    public void Fixture_NoMines_SurfacesWarning()
    {
        var s = WithSeededPlayerContent("random_hire_7");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "dragon_utopia" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues, i =>
            i.FieldKey == ValidationFieldKeys.ZoneContentPool
            && i.Code == ValidationIssueCodes.ContentPoolNoMines);
    }

    // ── IncludeList-based positive coverage (no warning when a list, not a
    //    raw SID, supplies the category) ─────────────────────────────────

    [Fact]
    public void IncludeList_UnitsBanks_CoversCreatureBankRule()
    {
        var s = WithSeededPlayerContent("random_hire_7");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler-banks",
            IncludeListIds = { "basic_content_list_building_guarded_units_banks" },
        });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "mine_gold" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler-resource",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.DoesNotContain(result.Issues, i =>
            i.Code == ValidationIssueCodes.ContentPoolNoCreatureBanks);
    }

    [Fact]
    public void IncludeList_RareMines_CoversMineRule()
    {
        var s = WithSeededPlayerContent("random_hire_7");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "dragon_utopia" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler-mines",
            IncludeListIds = { "basic_content_list_rare_mines_by_biome" },
        });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler-resource",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.DoesNotContain(result.Issues, i =>
            i.Code == ValidationIssueCodes.ContentPoolNoMines);
    }

    [Fact]
    public void GenericRandomHiresPool_CoversTier7Rule()
    {
        var s = WithSeededPlayerContent("random_hire_1");
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler-hires",
            IncludeListIds = { "content_list_building_random_hires" }, // open-ended pool
        });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "sacrificial_shrine" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "dragon_utopia" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "mine_gold" });
        s.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "filler-resource",
            IncludeListIds = { "basic_content_list_building_guarded_resource_banks_tier_1" },
        });

        var result = SettingsValidator.Validate(s);
        Assert.DoesNotContain(result.Issues, i =>
            i.Code == ValidationIssueCodes.ContentPoolNoTier7Dwellings);
    }

    // ── Per-tier content list also engages the sweep ────────────────────

    [Fact]
    public void NeutralByTier_PopulatedList_EngagesContentPoolSweep()
    {
        var s = ValidBaseline();
        var tierList = new ZoneContentList();
        tierList.Items.Add(new ZoneContentItem { Sid = "random_hire_1" });
        s.NeutralZoneContent.ByTier[NeutralZoneTier.Rich] = tierList;

        var result = SettingsValidator.Validate(s);
        // With only one low-tier hire, every content-pool warning except the
        // hire one should also fire — the gate is engaged.
        Assert.Contains(result.Issues, i =>
            i.Code == ValidationIssueCodes.ContentPoolNoTier7Dwellings);
        Assert.Contains(result.Issues, i =>
            i.Code == ValidationIssueCodes.ContentPoolNoShrines);
    }

    // ── Regression gate: every shipped preset stays content-pool clean ─

    [Fact]
    public void EveryPreset_StaysContentPoolClean()
    {
        var catalog = new PresetCatalog();
        Assert.NotEmpty(catalog.Entries);

        foreach (var entry in catalog.Entries)
        {
            var file = catalog.Load(entry.Id);
            var (settings, _, _, _) = SettingsMapper.FromFile(file);

            var result = SettingsValidator.Validate(settings);
            var contentPoolWarnings = result.Issues
                .Where(i => i.FieldKey == ValidationFieldKeys.ZoneContentPool)
                .Select(i => i.Message)
                .ToList();

            Assert.True(
                contentPoolWarnings.Count == 0,
                $"Preset '{entry.Id}' surfaced T-703 content-pool warnings: {string.Join("; ", contentPoolWarnings)}");
        }
    }
}
