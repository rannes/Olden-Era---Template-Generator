using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Tests;

public class ExperimentalSettingsTests
{
    [Fact]
    public void Defaults_DoNotEmitExperimentalFields()
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings());

        // Hero hire ban + bonuses + bans + custom limits should remain at the
        // pre-rollout shape: empty / null / single seed bonus.
        Assert.False(template.GameRules?.HeroHireBan ?? false);
        Assert.Single(template.GameRules!.Bonuses!); // movementBonus seed only
        Assert.Null(template.GlobalBans);

        var connections = template.Variants?[0].Connections;
        Assert.NotNull(connections);
        // Generator's hardcoded default is 0.15–0.20 — non-zero — so we just
        // assert the user override path didn't run by checking values stay
        // within the original distribution.
        Assert.All(connections!, c => Assert.True(c.GuardWeeklyIncrement is null or > 0));
    }

    [Fact]
    public void SingleHeroMode_ForcesHeroCountToOne()
    {
        var s = new GeneratorSettings
        {
            GameMode = "SingleHero",
            HeroSettings = new HeroSettings { HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.Equal(1, template.GameRules!.HeroCountMin);
        Assert.Equal(1, template.GameRules!.HeroCountMax);
        Assert.Equal(1, template.GameRules!.HeroCountIncrement);
    }

    [Fact]
    public void HeroHireBan_SetsGameRuleFlag()
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings { HeroHireBan = true });
        Assert.True(template.GameRules!.HeroHireBan);
    }

    [Fact]
    public void DesertionOverrides_UpdateWinConditions()
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings
        {
            DesertionDay = 7,
            DesertionValue = 5000,
        });
        Assert.Equal(7, template.GameRules!.WinConditions!.DesertionDay);
        Assert.Equal(5000, template.GameRules!.WinConditions!.DesertionValue);
    }

    [Fact]
    public void GlobalBans_AppearOnTemplate()
    {
        var s = new GeneratorSettings();
        s.Content.GlobalBans.Add("dragon_utopia");
        s.Content.GlobalBans.Add("pandora_box");

        var template = TemplateGenerator.Generate(s);
        Assert.NotNull(template.GlobalBans);
        Assert.Contains("dragon_utopia", template.GlobalBans!.Items!);
        Assert.Contains("pandora_box", template.GlobalBans!.Items!);
    }

    [Fact]
    public void ContentCountLimits_AppendUserEntries()
    {
        var s = new GeneratorSettings();
        s.Content.ContentCountLimits.Add(new ContentLimit { Sid = "mine_gold", MaxPerPlayer = 2 });

        var template = TemplateGenerator.Generate(s);
        Assert.Contains(template.ContentCountLimits!,
            l => l.Limits is not null
                 && l.Limits.Exists(x => x.Sid == "mine_gold" && x.MaxCount == 2));
    }

    [Fact]
    public void StartingBonuses_EmitOnePerSetField()
    {
        var s = new GeneratorSettings();
        s.Bonuses.Resources["gold"] = 5;
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.ItemSid = "some_item";
        s.Bonuses.SpellSid = "some_spell";
        s.Bonuses.UnitMultiplier = 1.5;

        var template = TemplateGenerator.Generate(s);
        var bonuses = template.GameRules!.Bonuses!;
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_res");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_stat" && b.Parameters![0] == "attack");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_item");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_spell");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_unit_multipler");
    }

    [Fact]
    public void StartHeroOnly_FlipsReceiverFilter()
    {
        var s = new GeneratorSettings();
        s.Bonuses.HeroAttack = 1;
        s.Bonuses.HeroStatStartHeroOnly = true;

        var template = TemplateGenerator.Generate(s);
        var stat = template.GameRules!.Bonuses!.Find(b => b.Sid == "add_bonus_hero_stat" && b.Parameters![0] == "attack");
        Assert.NotNull(stat);
        Assert.Equal("start_hero", stat!.ReceiverFilter);
    }

    [Fact]
    public void GuardProgressionOverride_StampsZonesAndConnections()
    {
        var s = new GeneratorSettings
        {
            GuardProgression = new GuardProgressionSettings
            {
                ZoneGuardWeeklyIncrement = 0.25,
                ConnectionGuardWeeklyIncrement = 0.30,
            },
        };
        var template = TemplateGenerator.Generate(s);
        var variant = template.Variants![0];
        Assert.All(variant.Zones!, z => Assert.Equal(0.25, z.GuardWeeklyIncrement));
        Assert.All(variant.Connections!, c => Assert.Equal(0.30, c.GuardWeeklyIncrement));
    }

    [Fact]
    public void TerrainOverrides_ApplyToZoneLayouts()
    {
        var s = new GeneratorSettings
        {
            Terrain = new TerrainSettings { ObstaclesFill = 0.4, LakesFill = 0.2 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.ZoneLayouts!, l => Assert.Equal(0.4, l.ObstaclesFill));
        Assert.All(template.ZoneLayouts!, l => Assert.Equal(0.2, l.LakesFill));
    }

    [Fact]
    public void BuildingPresetOverride_AppliesToCities()
    {
        var s = new GeneratorSettings
        {
            BuildingPresets = new BuildingPresetSettings
            {
                PlayerZonePreset = "rich_buildings_construction",
                NeutralZonePreset = "poor_buildings_construction",
            },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
        };
        var template = TemplateGenerator.Generate(s);
        // Player-zone Spawn objects pick up the player preset.
        var spawns = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "Spawn")
            .ToList();
        Assert.NotEmpty(spawns);
        Assert.All(spawns, m => Assert.Equal("rich_buildings_construction", m.BuildingsConstructionSid));

        // Neutral City objects pick up the neutral preset.
        var neutralCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(neutralCities);
        Assert.All(neutralCities, m => Assert.Equal("poor_buildings_construction", m.BuildingsConstructionSid));
    }

    [Fact]
    public void TierOverride_BuildingPresetWinsOverGlobalForMatchingTier()
    {
        var s = new GeneratorSettings
        {
            BuildingPresets = new BuildingPresetSettings { NeutralZonePreset = "poor_buildings_construction" },
        };
        s.ZoneCfg.Advanced.Enabled = true;
        s.ZoneCfg.Advanced.NeutralHighCastleCount = 1; // Spawn one High-tier neutral zone with a castle.
        s.ZoneCfg.Advanced.HighTier.BuildingPreset = "ultra_rich_buildings_construction";

        var template = TemplateGenerator.Generate(s);
        var allCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(allCities);
        // High tier zone(s) must use the ultra_rich override; everything else stays on the global poor.
        Assert.Contains(allCities, m => m.BuildingsConstructionSid == "ultra_rich_buildings_construction");
    }

    [Fact]
    public void TierOverride_GuardWeeklyIncrementWinsForMatchingTier()
    {
        var s = new GeneratorSettings
        {
            GuardProgression = new GuardProgressionSettings { ZoneGuardWeeklyIncrement = 0.10 },
        };
        s.ZoneCfg.Advanced.Enabled = true;
        s.ZoneCfg.Advanced.NeutralLowNoCastleCount = 1;
        s.ZoneCfg.Advanced.LowTier.GuardWeeklyIncrement = 0.40;

        var template = TemplateGenerator.Generate(s);
        var lowZones = template.Variants![0].Zones!.Where(z => z.GuardWeeklyIncrement == 0.40).ToList();
        Assert.NotEmpty(lowZones);
    }

    [Fact]
    public void NeutralCities_GuardChance_OverridesNeutralCityGuardChance()
    {
        var s = new GeneratorSettings
        {
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
            NeutralCities = new NeutralCitySettings { GuardChance = 0.3 },
        };
        var template = TemplateGenerator.Generate(s);
        var neutralCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(neutralCities);
        Assert.All(neutralCities, m => Assert.Equal(0.3, m.GuardChance));
        // Defense in depth: setting GuardChance must not implicitly drag
        // RemoveGuardIfHasOwner along (the two fields are independent).
        Assert.All(neutralCities, m => Assert.Null(m.RemoveGuardIfHasOwner));

        // Spawn (player) cities are unaffected — still fully guarded.
        var spawns = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "Spawn")
            .ToList();
        Assert.NotEmpty(spawns);
        Assert.All(spawns, m => Assert.Equal(1.0, m.GuardChance));
    }

    [Fact]
    public void NeutralCities_RemoveGuardIfHasOwner_EmitsOnNeutralOnly()
    {
        var s = new GeneratorSettings
        {
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
            NeutralCities = new NeutralCitySettings { RemoveGuardIfHasOwner = true },
        };
        var template = TemplateGenerator.Generate(s);
        var neutralCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(neutralCities);
        Assert.All(neutralCities, m => Assert.True(m.RemoveGuardIfHasOwner));
    }

    [Fact]
    public void NeutralCities_DefaultSettings_DoNotEmitRemoveGuardIfHasOwner_OnNeutralCities()
    {
        // Default behavior must remain byte-identical: neutral City entries should not have removeGuardIfHasOwner set.
        var s = new GeneratorSettings
        {
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
        };
        var template = TemplateGenerator.Generate(s);
        var neutralCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(neutralCities);
        Assert.All(neutralCities, m => Assert.Null(m.RemoveGuardIfHasOwner));
    }

    [Fact]
    public void SettingsMapper_RoundTripsExperimentalFields()
    {
        var g = new GeneratorSettings
        {
            GameMode = "SingleHero",
            HeroHireBan = true,
            DesertionDay = 5,
            DesertionValue = 4500,
            Terrain = new TerrainSettings { ObstaclesFill = 0.3, LakesFill = 0.1 },
            BuildingPresets = new BuildingPresetSettings { PlayerZonePreset = "rich_buildings_construction" },
            GuardProgression = new GuardProgressionSettings { ZoneGuardWeeklyIncrement = 0.2 },
            NeutralCities = new NeutralCitySettings { GuardChance = 0.75, GuardValuePercent = 150, RemoveGuardIfHasOwner = true },
        };
        g.Content.GlobalBans.Add("dragon_utopia");
        g.Content.ContentCountLimits.Add(new ContentLimit { Sid = "mine_gold", MaxPerPlayer = 3 });
        g.Bonuses.Resources["wood"] = 4;
        g.Bonuses.HeroSpellpower = 3;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal("SingleHero", back.GameMode);
        Assert.True(back.HeroHireBan);
        Assert.Equal(5, back.DesertionDay);
        Assert.Equal(4500, back.DesertionValue);
        Assert.Equal(0.3, back.Terrain.ObstaclesFill);
        Assert.Equal(0.1, back.Terrain.LakesFill);
        Assert.Equal("rich_buildings_construction", back.BuildingPresets.PlayerZonePreset);
        Assert.Equal(0.2, back.GuardProgression.ZoneGuardWeeklyIncrement);
        Assert.Equal(0.75, back.NeutralCities.GuardChance);
        Assert.Equal(150, back.NeutralCities.GuardValuePercent);
        Assert.True(back.NeutralCities.RemoveGuardIfHasOwner);
        Assert.Contains("dragon_utopia", back.Content.GlobalBans);
        Assert.Single(back.Content.ContentCountLimits);
        Assert.Equal(3, back.Content.ContentCountLimits[0].MaxPerPlayer);
        Assert.Equal(4, back.Bonuses.Resources["wood"]);
        Assert.Equal(3, back.Bonuses.HeroSpellpower);
    }

    [Fact]
    public void SettingsMapper_BordersRoads_RoundTrips()
    {
        var original = new GeneratorSettings
        {
            BordersRoads = new BordersRoadsSettings
            {
                CornerRadius = 0.3,
                ObstaclesWidth = 5,
                WaterBorderEnabled = true,
                WaterWidth = 7,
                RoadType = "Stone"
            }
        };

        var file = SettingsMapper.ToFile(original, advancedMode: false, experimentalMapSizes: false);
        var (restored, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(0.3, restored.BordersRoads.CornerRadius);
        Assert.Equal(5, restored.BordersRoads.ObstaclesWidth);
        Assert.True(restored.BordersRoads.WaterBorderEnabled);
        Assert.Equal(7, restored.BordersRoads.WaterWidth);
        Assert.Equal("Stone", restored.BordersRoads.RoadType);
    }

    [Fact]
    public void SettingsMapper_BordersRoads_DefaultsRoundTripAsUnset()
    {
        var original = new GeneratorSettings();

        var file = SettingsMapper.ToFile(original, advancedMode: false, experimentalMapSizes: false);
        var (restored, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Null(restored.BordersRoads.CornerRadius);
        Assert.Null(restored.BordersRoads.ObstaclesWidth);
        Assert.False(restored.BordersRoads.WaterBorderEnabled);
        Assert.Null(restored.BordersRoads.RoadType);
    }

    // ── T-005: per-zone schema knobs (diplomacyModifier, crossroadsPosition, contentBiome)

    [Fact]
    public void ZoneOverrides_Default_LeavesGeneratorBakedValues()
    {
        // Snapshot current generator output: every zone today emits
        // diplomacyModifier=-0.5, crossroadsPosition=0, and a generator-chosen
        // contentBiome. The default GeneratorSettings must not alter that.
        var defaultTemplate = TemplateGenerator.Generate(new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 }
        });
        var zones = defaultTemplate.Variants![0].Zones!;
        Assert.All(zones, z => Assert.Equal(-0.5, z.DiplomacyModifier));
        Assert.All(zones, z => Assert.Equal(0, z.CrossroadsPosition));
        Assert.All(zones, z => Assert.NotNull(z.ContentBiome)); // generator default present
    }

    [Fact]
    public void ZoneOverrides_DiplomacyModifier_StampsEveryZone()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
            ZoneOverrides = new ZoneOverridesSettings { DiplomacyModifier = 0.25 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(0.25, z.DiplomacyModifier));
        // Other knobs untouched.
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(0, z.CrossroadsPosition));
    }

    [Fact]
    public void ZoneOverrides_CrossroadsPosition_StampsEveryZone()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
            ZoneOverrides = new ZoneOverridesSettings { CrossroadsPosition = 1 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(1, z.CrossroadsPosition));
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(-0.5, z.DiplomacyModifier));
    }

    [Fact]
    public void ZoneOverrides_ContentBiome_FromList_ReplacesPerZoneSelector()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
            ZoneOverrides = new ZoneOverridesSettings
            {
                ContentBiomeType = "FromList",
                ContentBiomeArg = "Sand"
            },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z =>
        {
            Assert.Equal("FromList", z.ContentBiome!.Type);
            Assert.Single(z.ContentBiome.Args!, "Sand");
        });
    }

    [Fact]
    public void ZoneOverrides_ContentBiome_MatchZone_OmitsArgs()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneOverrides = new ZoneOverridesSettings
            {
                ContentBiomeType = "MatchZone",
                ContentBiomeArg = "ignored"
            },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z =>
        {
            Assert.Equal("MatchZone", z.ContentBiome!.Type);
            Assert.Empty(z.ContentBiome.Args!);
        });
    }

    [Fact]
    public void ZoneOverrides_ContentBiome_ClonedPerZone_NoAliasing()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneOverrides = new ZoneOverridesSettings
            {
                ContentBiomeType = "FromList",
                ContentBiomeArg = "Deathland"
            },
        };
        var template = TemplateGenerator.Generate(s);
        var zones = template.Variants![0].Zones!;
        // Mutating one zone must not affect siblings.
        zones[0].ContentBiome!.Args!.Add("extra");
        Assert.DoesNotContain("extra", zones[1].ContentBiome!.Args!);
    }

    [Fact]
    public void SettingsMapper_ZoneOverrides_RoundTripsValues()
    {
        var original = new GeneratorSettings
        {
            ZoneOverrides = new ZoneOverridesSettings
            {
                DiplomacyModifier = -0.25,
                CrossroadsPosition = 2,
                ContentBiomeType = "FromList",
                ContentBiomeArg = "Sand",
            },
        };
        var file = SettingsMapper.ToFile(original, advancedMode: false, experimentalMapSizes: false);
        var (restored, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(-0.25, restored.ZoneOverrides.DiplomacyModifier);
        Assert.Equal(2, restored.ZoneOverrides.CrossroadsPosition);
        Assert.Equal("FromList", restored.ZoneOverrides.ContentBiomeType);
        Assert.Equal("Sand", restored.ZoneOverrides.ContentBiomeArg);
    }

    [Fact]
    public void SettingsMapper_ZoneOverrides_DefaultsRoundTripAsUnset()
    {
        var file = SettingsMapper.ToFile(new GeneratorSettings(), advancedMode: false, experimentalMapSizes: false);
        var (restored, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Null(restored.ZoneOverrides.DiplomacyModifier);
        Assert.Null(restored.ZoneOverrides.CrossroadsPosition);
        Assert.Equal("", restored.ZoneOverrides.ContentBiomeType);
        Assert.Equal("", restored.ZoneOverrides.ContentBiomeArg);
    }

    [Fact]
    public void SettingsShareCodec_ZoneOverrides_RoundTripsAcrossEncoded()
    {
        var file = new SettingsFile
        {
            ZoneDiplomacyModifier = 0.1,
            ZoneCrossroadsPosition = 3,
            ZoneContentBiomeType = "MatchMainObject",
            ZoneContentBiomeArg = "0",
        };
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Equal(0.1, decoded!.ZoneDiplomacyModifier);
        Assert.Equal(3, decoded.ZoneCrossroadsPosition);
        Assert.Equal("MatchMainObject", decoded.ZoneContentBiomeType);
        Assert.Equal("0", decoded.ZoneContentBiomeArg);
    }

    // ── T-006: per-zone caps / cutoff / content pools ──────────────────────

    [Fact]
    public void ZoneOverrides_T006_DefaultsAreNoOp()
    {
        // Acceptance: with no T-006 override set, no zone gains a custom
        // contentCountLimits / pool / cutoff override beyond what the builder
        // already wrote. We assert byte-identity by serialising both templates
        // with the same options — locks in true equivalence rather than just
        // a hand-picked field set.
        var seed = 12345;
        var a = TemplateGenerator.Generate(new GeneratorSettings { PlayerCount = 2, Seed = seed });
        var b = TemplateGenerator.Generate(new GeneratorSettings
        {
            PlayerCount = 2,
            Seed = seed,
            ZoneOverrides = new ZoneOverridesSettings(), // all defaults
        });

        var opts = new JsonSerializerOptions { WriteIndented = false };
        Assert.Equal(JsonSerializer.Serialize(a, opts), JsonSerializer.Serialize(b, opts));
    }

    [Fact]
    public void ZoneOverrides_T006_GuardCutoff_StampsEveryZone()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneOverrides = new ZoneOverridesSettings { GuardCutoffValue = 4242 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z => Assert.Equal(4242, z.GuardCutoffValue));
    }

    [Fact]
    public void ZoneOverrides_T006_ContentPools_ReplaceBuilderChoice()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneOverrides = new ZoneOverridesSettings
            {
                GuardedContentPool = new() { "template_pool_custom_guarded" },
                UnguardedContentPool = new() { "template_pool_custom_unguarded_a", "template_pool_custom_unguarded_b" },
            },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z =>
        {
            Assert.Equal(new[] { "template_pool_custom_guarded" }, z.GuardedContentPool);
            Assert.Equal(new[] { "template_pool_custom_unguarded_a", "template_pool_custom_unguarded_b" }, z.UnguardedContentPool);
        });
    }

    [Fact]
    public void ZoneOverrides_T006_ContentPools_ClonedPerZone_NoAliasing()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneOverrides = new ZoneOverridesSettings
            {
                GuardedContentPool = new() { "pool_a" },
            },
        };
        var template = TemplateGenerator.Generate(s);
        var zones = template.Variants![0].Zones!;
        zones[0].GuardedContentPool!.Add("extra");
        Assert.DoesNotContain("extra", zones[1].GuardedContentPool!);
    }

    [Fact]
    public void ZoneOverrides_T006_ContentCountLimits_OverrideZoneBuilderDefaults()
    {
        var s = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneOverrides = new ZoneOverridesSettings
            {
                ContentCountLimitRefs = new() { "content_limits_custom" },
            },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.Variants![0].Zones!, z =>
            Assert.Equal(new[] { "content_limits_custom" }, z.ContentCountLimits));
    }

    [Fact]
    public void SettingsMapper_T006_RoundTripsValues()
    {
        var original = new GeneratorSettings
        {
            ZoneOverrides = new ZoneOverridesSettings
            {
                GuardCutoffValue = 2500,
                GuardedContentPool = new() { "g1", "g2" },
                UnguardedContentPool = new() { "u1" },
                ContentCountLimitRefs = new() { "content_limits_spawn", "content_limits_side" },
            },
        };
        var file = SettingsMapper.ToFile(original, advancedMode: false, experimentalMapSizes: false);
        // The CSV strings on the wire shape are the load-bearing contract for
        // share-code round-trip; assert them explicitly.
        Assert.Equal(2500, file.ZoneGuardCutoffValue);
        Assert.Equal("g1,g2", file.ZoneGuardedContentPool);
        Assert.Equal("u1", file.ZoneUnguardedContentPool);
        Assert.Equal("content_limits_spawn,content_limits_side", file.ZoneContentCountLimits);

        var (restored, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Equal(2500, restored.ZoneOverrides.GuardCutoffValue);
        Assert.Equal(new[] { "g1", "g2" }, restored.ZoneOverrides.GuardedContentPool);
        Assert.Equal(new[] { "u1" }, restored.ZoneOverrides.UnguardedContentPool);
        Assert.Equal(new[] { "content_limits_spawn", "content_limits_side" },
            restored.ZoneOverrides.ContentCountLimitRefs);
    }

    [Fact]
    public void SettingsMapper_T006_DefaultsRoundTripAsUnset()
    {
        var file = SettingsMapper.ToFile(new GeneratorSettings(), advancedMode: false, experimentalMapSizes: false);
        Assert.Null(file.ZoneGuardCutoffValue);
        Assert.Equal("", file.ZoneGuardedContentPool);
        Assert.Equal("", file.ZoneUnguardedContentPool);
        Assert.Equal("", file.ZoneContentCountLimits);

        var (restored, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Null(restored.ZoneOverrides.GuardCutoffValue);
        Assert.Empty(restored.ZoneOverrides.GuardedContentPool);
        Assert.Empty(restored.ZoneOverrides.UnguardedContentPool);
        Assert.Empty(restored.ZoneOverrides.ContentCountLimitRefs);
    }

    [Fact]
    public void SettingsShareCodec_T006_RoundTripsAcrossEncoded()
    {
        // The share codec only round-trips scalars / strings — lists must travel
        // as CSV. This test guards that contract.
        var file = new SettingsFile
        {
            ZoneGuardCutoffValue = 2200,
            ZoneGuardedContentPool = "pool_a,pool_b",
            ZoneUnguardedContentPool = "pool_u",
            ZoneContentCountLimits = "content_limits_center",
        };
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Equal(2200, decoded!.ZoneGuardCutoffValue);
        Assert.Equal("pool_a,pool_b", decoded.ZoneGuardedContentPool);
        Assert.Equal("pool_u", decoded.ZoneUnguardedContentPool);
        Assert.Equal("content_limits_center", decoded.ZoneContentCountLimits);
    }
}
