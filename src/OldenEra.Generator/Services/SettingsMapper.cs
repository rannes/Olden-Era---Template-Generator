using System.Collections.Generic;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services;

/// <summary>
/// Translates between the persisted <see cref="SettingsFile"/> shape and the
/// in-memory <see cref="GeneratorSettings"/> shape used by the panels. This is
/// the web equivalent of MainWindow's GatherSettings/ApplySettings/BuildSettings
/// trio — there's only one in-memory model in the WASM app, so we map directly.
/// </summary>
/// <summary>
/// T-302 — bag of per-feature experimental flags reconstructed from a loaded
/// <see cref="SettingsFile"/>. Indexed by the keys in
/// <see cref="ExperimentalFeatures"/>. Plain dictionary instead of named
/// fields so adding a feature only touches the registry + SettingsFile.
/// </summary>
public sealed class ExperimentalFlags
{
    public bool GameMode         { get; set; }
    public bool StartingBonuses  { get; set; }
    public bool ZoneContent      { get; set; }
    public bool BordersRoads     { get; set; }
    public bool PerTierOverrides { get; set; }

    public bool AnyEnabled =>
        GameMode || StartingBonuses || ZoneContent || BordersRoads || PerTierOverrides;

    public bool Get(string key) => key switch
    {
        ExperimentalFeatures.GameMode         => GameMode,
        ExperimentalFeatures.StartingBonuses  => StartingBonuses,
        ExperimentalFeatures.ZoneContent      => ZoneContent,
        ExperimentalFeatures.BordersRoads     => BordersRoads,
        ExperimentalFeatures.PerTierOverrides => PerTierOverrides,
        _ => throw new ArgumentException($"Unknown experimental feature key: {key}", nameof(key)),
    };

    public void Set(string key, bool value)
    {
        switch (key)
        {
            case ExperimentalFeatures.GameMode:         GameMode = value; break;
            case ExperimentalFeatures.StartingBonuses:  StartingBonuses = value; break;
            case ExperimentalFeatures.ZoneContent:      ZoneContent = value; break;
            case ExperimentalFeatures.BordersRoads:     BordersRoads = value; break;
            case ExperimentalFeatures.PerTierOverrides: PerTierOverrides = value; break;
            default:
                throw new ArgumentException($"Unknown experimental feature key: {key}", nameof(key));
        }
    }

    public ExperimentalFlags Clone() => new()
    {
        GameMode = GameMode,
        StartingBonuses = StartingBonuses,
        ZoneContent = ZoneContent,
        BordersRoads = BordersRoads,
        PerTierOverrides = PerTierOverrides,
    };
}

public static class SettingsMapper
{
    /// <summary>
    /// Build a fresh <see cref="GeneratorSettings"/> from a loaded file.
    /// Returns the new settings and the reconstructed advanced/experimental flags.
    /// </summary>
    public static (GeneratorSettings Settings, bool AdvancedMode, bool ExperimentalMapSizes, ExperimentalFlags Experimental) FromFile(SettingsFile s)
    {
        bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001
                                || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
        bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
        bool advanced = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;

        // Migration: pre-Balanced files used `Topology=Random` + the legacy
        // `experimentalBalancedZonePlacement` flag to mean the same thing as today's
        // `MapTopology.Balanced`. Promote to the new topology on load.
        var migratedTopology = s.Topology == MapTopology.Random && s.ExperimentalBalancedZonePlacement
            ? MapTopology.Balanced
            : s.Topology;

        var settings = new GeneratorSettings
        {
            TemplateName = string.IsNullOrEmpty(s.TemplateName) ? "Custom Template" : s.TemplateName,
            // T-504 — user overrides; "" = auto-generated. Pass through verbatim.
            Description = s.DescriptionOverride ?? "",
            DisplayWinCondition = s.DisplayWinConditionOverride ?? "",
            GameMode = string.IsNullOrEmpty(s.GameMode) ? "Classic" : s.GameMode,
            MapSize = s.MapSize,
            PlayerCount = s.PlayerCount,
            HeroHireBan = s.HeroHireBan,
            DesertionDay = s.DesertionDay,
            DesertionValue = s.DesertionValue,
            Terrain = new TerrainSettings
            {
                ObstaclesFill = s.TerrainObstaclesFill,
                LakesFill = s.TerrainLakesFill,
            },
            BordersRoads = new BordersRoadsSettings
            {
                CornerRadius = s.BorderCornerRadius,
                ObstaclesWidth = s.BorderObstaclesWidth,
                WaterBorderEnabled = s.WaterBorderEnabled,
                WaterWidth = s.WaterWidth,
                RoadType = string.IsNullOrEmpty(s.RoadType) ? null : s.RoadType
            },
            ZoneOverrides = new ZoneOverridesSettings
            {
                DiplomacyModifier = s.ZoneDiplomacyModifier,
                CrossroadsPosition = s.ZoneCrossroadsPosition,
                ContentBiomeType = s.ZoneContentBiomeType ?? "",
                ContentBiomeArg = s.ZoneContentBiomeArg ?? "",
                MetaObjectsBiomeType = s.ZoneMetaObjectsBiomeType ?? "",
                MetaObjectsBiomeArg = s.ZoneMetaObjectsBiomeArg ?? "",
                GuardCutoffValue = s.ZoneGuardCutoffValue,
                GuardedContentPool = SidCsv.Parse(s.ZoneGuardedContentPool),
                UnguardedContentPool = SidCsv.Parse(s.ZoneUnguardedContentPool),
                ContentCountLimitRefs = SidCsv.Parse(s.ZoneContentCountLimits),
                GuardMultiplier = s.ZoneGuardMultiplier,
                GuardRandomization = s.ZoneGuardRandomization,
                // T-503 — per-zone content/resource value overrides.
                ResourcesValue                = s.ZoneResourcesValue,
                ResourcesValuePerArea         = s.ZoneResourcesValuePerArea,
                GuardedContentValue           = s.ZoneGuardedContentValue,
                GuardedContentValuePerArea    = s.ZoneGuardedContentValuePerArea,
                UnguardedContentValue         = s.ZoneUnguardedContentValue,
                UnguardedContentValuePerArea  = s.ZoneUnguardedContentValuePerArea,
            },
            EncounterHoles = new EncounterHolesOptions
            {
                Enabled = s.EncounterHolesEnabled,
                AffectedEncounters = s.EncounterHolesAffectedEncounters,
                TwoHoleEncounters = s.EncounterHolesTwoHoleEncounters,
            },
            BuildingPresets = new BuildingPresetSettings
            {
                PlayerZonePreset = s.BuildingPresetPlayer ?? "",
                NeutralZonePreset = s.BuildingPresetNeutral ?? "",
            },
            GuardProgression = new GuardProgressionSettings
            {
                ZoneGuardWeeklyIncrement = s.ZoneGuardWeeklyIncrement,
                ConnectionGuardWeeklyIncrement = s.ConnectionGuardWeeklyIncrement,
            },
            GuardReaction = new GuardReactionSettings
            {
                Preset = ParseGuardReactionPreset(s.GuardReactionPreset),
                CustomDistribution = ParseDistributionCsv(s.GuardReactionCustomDistribution),
            },
            ConnectionDefaults = new ConnectionDefaultsSettings
            {
                Length = s.ConnectionLength,
                GatePlacement = s.ConnectionGatePlacement ?? "",
                GuardEscape = s.ConnectionGuardEscape,
                SimTurnSquad = s.ConnectionSimTurnSquad,
                GuardRandomization = s.ConnectionGuardRandomization,
            },
            NeutralCities = new NeutralCitySettings
            {
                GuardChance = s.NeutralCityGuardChance,
                GuardValuePercent = s.NeutralCityGuardValuePercent <= 0 ? 100 : s.NeutralCityGuardValuePercent,
                RemoveGuardIfHasOwner = s.NeutralCityRemoveGuardIfHasOwner,
            },
            Content = new ContentControlSettings
            {
                GlobalBans = s.GlobalBans is null ? new() : new List<string>(s.GlobalBans),
                ContentCountLimits = s.ContentCountLimits is null
                    ? new()
                    : s.ContentCountLimits.ConvertAll(l => new ContentLimit { Sid = l.Sid, MaxPerPlayer = l.MaxPerPlayer }),
                ValueOverrides = s.ValueOverrides is null
                    ? new()
                    : s.ValueOverrides.ConvertAll(v => new ValueOverrideSetting
                    {
                        Sid = v.Sid ?? "",
                        Variant = v.Variant,
                        GuardValue = v.GuardValue,
                    }),
            },
            Bonuses = new StartingBonusSettings
            {
                Resources = s.BonusResources is null ? new() : new Dictionary<string,int>(s.BonusResources),
                HeroAttack = s.BonusHeroAttack,
                HeroDefense = s.BonusHeroDefense,
                HeroSpellpower = s.BonusHeroSpellpower,
                HeroKnowledge = s.BonusHeroKnowledge,
                HeroStatStartHeroOnly = s.BonusHeroStatStartHeroOnly,
                ItemSid = s.BonusItemSid ?? "",
                ItemStartHeroOnly = s.BonusItemStartHeroOnly,
                SpellSid = s.BonusSpellSid ?? "",
                SpellStartHeroOnly = s.BonusSpellStartHeroOnly,
                UnitMultiplier = s.BonusUnitMultiplier,
                UnitMultiplierStartHeroOnly = s.BonusUnitMultiplierStartHeroOnly,
                PerPlayerOverrides = s.BonusPerPlayerOverrides is null
                    ? new()
                    : s.BonusPerPlayerOverrides.ConvertAll(o => new PerPlayerBonusOverride
                    {
                        PlayerSlot = o.PlayerSlot,
                        Bonuses = new StartingBonusSettings
                        {
                            Resources = o.Resources is null ? new() : new Dictionary<string,int>(o.Resources),
                            HeroAttack = o.HeroAttack,
                            HeroDefense = o.HeroDefense,
                            HeroSpellpower = o.HeroSpellpower,
                            HeroKnowledge = o.HeroKnowledge,
                            HeroStatStartHeroOnly = o.HeroStatStartHeroOnly,
                            ItemSid = o.ItemSid ?? "",
                            ItemStartHeroOnly = o.ItemStartHeroOnly,
                            SpellSid = o.SpellSid ?? "",
                            SpellStartHeroOnly = o.SpellStartHeroOnly,
                            UnitMultiplier = o.UnitMultiplier,
                            UnitMultiplierStartHeroOnly = o.UnitMultiplierStartHeroOnly,
                        },
                    }),
            },
            HeroSettings = new HeroSettings
            {
                HeroCountMin = s.HeroCountMin,
                HeroCountMax = s.HeroCountMax,
                HeroCountIncrement = s.HeroCountIncrement,
                HeroBans = s.HeroBans is null ? new() : new List<string>(s.HeroBans),
                BannedSpells = s.BannedSpells is null ? new() : new List<string>(s.BannedSpells),
                FixedStartingHeroByFaction = s.FixedStartingHeroByFaction is null
                    ? new()
                    : new Dictionary<string, string?>(s.FixedStartingHeroByFaction),
            },
            Topology = migratedTopology,
            RandomPortals = s.RandomPortals,
            MaxPortalConnections = Math.Clamp(s.MaxPortalConnections, 1, 32),
            SpawnRemoteFootholds = s.SpawnRemoteFootholds,
            GenerateRoads = s.GenerateRoads,
            NoDirectPlayerConnections = s.NoDirectPlayerConn,
            MatchPlayerCastleFactions = s.MatchPlayerCastleFactions,
            MinNeutralZonesBetweenPlayers = s.MinNeutralZonesBetweenPlayers,
            FactionLawsExpPercent = Math.Clamp(s.FactionLawsExpPercent, 25, 200),
            AstrologyExpPercent = Math.Clamp(s.AstrologyExpPercent, 25, 200),
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = s.NeutralZoneCount,
                PlayerZoneCastles = s.PlayerZoneCastles,
                NeutralZoneCastles = s.NeutralZoneCastles,
                ResourceDensityPercent = s.EffectiveResourceDensityPercent,
                StructureDensityPercent = s.EffectiveStructureDensityPercent,
                NeutralStackStrengthPercent = s.NeutralStackStrengthPercent,
                BorderGuardStrengthPercent = s.BorderGuardStrengthPercent,
                HubZoneSize = Math.Clamp(s.HubZoneSize, 0.25, 3.0),
                HubZoneCastles = Math.Clamp(s.HubZoneCastles, 0, 4),
                Advanced = new AdvancedSettings
                {
                    Enabled = advanced,
                    NeutralLowNoCastleCount = s.NeutralLowNoCastleCount,
                    NeutralLowCastleCount = s.NeutralLowCastleCount,
                    NeutralMediumNoCastleCount = s.NeutralMediumNoCastleCount,
                    NeutralMediumCastleCount = s.NeutralMediumCastleCount,
                    NeutralHighNoCastleCount = s.NeutralHighNoCastleCount,
                    NeutralHighCastleCount = s.NeutralHighCastleCount,
                    PlayerZoneSize = Math.Clamp(s.PlayerZoneSize, 0.1, 2.0),
                    NeutralZoneSize = Math.Clamp(s.NeutralZoneSize, 0.1, 2.0),
                    GuardRandomization = s.GuardRandomization,
                    LowTier = TierFromFile(s.TierLow),
                    MediumTier = TierFromFile(s.TierMedium),
                    HighTier = TierFromFile(s.TierHigh),
                },
            },
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = string.IsNullOrEmpty(s.VictoryCondition) ? "win_condition_1" : s.VictoryCondition,
                LostStartCity = s.LostStartCity,
                LostStartCityDay = Math.Clamp(s.LostStartCityDay, 1, 30),
                LostStartHero = s.LostStartHero,
                CityHold = s.CityHold,
                CityHoldDays = Math.Clamp(s.CityHoldDays, 1, 30),
                // T-506 — round-trip hero-lighting toggle and day.
                HeroLighting = s.HeroLighting,
                HeroLightingDay = Math.Clamp(s.HeroLightingDay, 1, 30),
            },
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = s.GladiatorArena,
                DaysDelayStart = Math.Clamp(s.GladiatorArenaDaysDelayStart, 1, 60),
                CountDay = Math.Clamp(s.GladiatorArenaCountDay, 1, 30),
            },
            TournamentRules = new TournamentRules
            {
                Enabled = s.Tournament,
                FirstTournamentDay = Math.Clamp(s.TournamentFirstTournamentDay, 1, 60),
                Interval = Math.Clamp(s.TournamentInterval, 1, 30),
                PointsToWin = Math.Clamp(s.TournamentPointsToWin, 1, 10),
                SaveArmy = s.TournamentSaveArmy,
            },
        };

        // Reference-shared: nested DTO trees are owned by the deserialized side after assignment.
        // Round 4 UI must clone before mutating if it cares about file/in-memory aliasing.
        settings.PlayerZoneContent   = s.PlayerZoneContent   ?? new();
        settings.NeutralZoneContent  = s.NeutralZoneContent  ?? new();
        settings.ZoneRoadDecorations = s.ZoneRoadDecorations ?? new();

        var flags = new ExperimentalFlags
        {
            GameMode         = s.ExpFeatureGameMode,
            StartingBonuses  = s.ExpFeatureStartingBonuses,
            ZoneContent      = s.ExpFeatureZoneContent,
            BordersRoads     = s.ExpFeatureBordersRoads,
            PerTierOverrides = s.ExpFeaturePerTierOverrides,
        };

        // T-302 migration: pre-T-302 .oetgs files had only the master flag.
        // If the master is on but no per-feature flag is set, light all five
        // so users keep access to whatever they were already using.
        if (s.ExperimentalEnabled && !flags.AnyEnabled)
        {
            flags.GameMode = true;
            flags.StartingBonuses = true;
            flags.ZoneContent = true;
            flags.BordersRoads = true;
            flags.PerTierOverrides = true;
        }

        return (settings, advanced, needsExperimentalMapSizes, flags);
    }

    /// <summary>
    /// Capture the current in-memory state into a <see cref="SettingsFile"/>
    /// so it can be JSON-serialized.
    /// </summary>
    public static SettingsFile ToFile(GeneratorSettings g, bool advancedMode, bool experimentalMapSizes, ExperimentalFlags? experimental = null)
    {
        experimental ??= new ExperimentalFlags();
        var a = g.ZoneCfg.Advanced;
        var file = new SettingsFile
        {
            TemplateName = g.TemplateName,
            // T-504 — user overrides; "" = auto-generated.
            DescriptionOverride         = g.Description ?? "",
            DisplayWinConditionOverride = g.DisplayWinCondition ?? "",
            MapSize = g.MapSize,
            PlayerCount = g.PlayerCount,
            NeutralZoneCount = g.ZoneCfg.NeutralZoneCount,
            PlayerZoneCastles = g.ZoneCfg.PlayerZoneCastles,
            NeutralZoneCastles = g.ZoneCfg.NeutralZoneCastles,
            AdvancedMode = advancedMode,
            NeutralLowNoCastleCount = a.NeutralLowNoCastleCount,
            NeutralLowCastleCount = a.NeutralLowCastleCount,
            NeutralMediumNoCastleCount = a.NeutralMediumNoCastleCount,
            NeutralMediumCastleCount = a.NeutralMediumCastleCount,
            NeutralHighNoCastleCount = a.NeutralHighNoCastleCount,
            NeutralHighCastleCount = a.NeutralHighCastleCount,
            MatchPlayerCastleFactions = g.MatchPlayerCastleFactions,
            MinNeutralZonesBetweenPlayers = g.MinNeutralZonesBetweenPlayers,
            // ExperimentalBalancedZonePlacement: legacy field, no longer written. Kept on
            // SettingsFile for back-compat reads only; SettingsMapper.FromFile migrates it.
            ExperimentalMapSizes = experimentalMapSizes,
            PlayerZoneSize = a.PlayerZoneSize,
            NeutralZoneSize = a.NeutralZoneSize,
            HubZoneSize = g.ZoneCfg.HubZoneSize,
            HubZoneCastles = g.ZoneCfg.HubZoneCastles,
            GuardRandomization = a.GuardRandomization,
            HeroCountMin = g.HeroSettings.HeroCountMin,
            HeroCountMax = g.HeroSettings.HeroCountMax,
            HeroCountIncrement = g.HeroSettings.HeroCountIncrement,
            HeroBans = new List<string>(g.HeroSettings.HeroBans),
            BannedSpells = new List<string>(g.HeroSettings.BannedSpells),
            FixedStartingHeroByFaction = new Dictionary<string, string?>(g.HeroSettings.FixedStartingHeroByFaction),
            Topology = g.Topology,
            RandomPortals = g.RandomPortals,
            MaxPortalConnections = g.MaxPortalConnections,
            SpawnRemoteFootholds = g.SpawnRemoteFootholds,
            GenerateRoads = g.GenerateRoads,
            NoDirectPlayerConn = g.NoDirectPlayerConnections,
            ResourceDensityPercent = g.ZoneCfg.ResourceDensityPercent,
            StructureDensityPercent = g.ZoneCfg.StructureDensityPercent,
            NeutralStackStrengthPercent = g.ZoneCfg.NeutralStackStrengthPercent,
            BorderGuardStrengthPercent = g.ZoneCfg.BorderGuardStrengthPercent,
            VictoryCondition = g.GameEndConditions.VictoryCondition,
            FactionLawsExpPercent = g.FactionLawsExpPercent,
            AstrologyExpPercent = g.AstrologyExpPercent,
            LostStartCity = g.GameEndConditions.LostStartCity,
            LostStartCityDay = g.GameEndConditions.LostStartCityDay,
            LostStartHero = g.GameEndConditions.LostStartHero,
            CityHold = g.GameEndConditions.CityHold,
            CityHoldDays = g.GameEndConditions.CityHoldDays,
            // T-506 — persist hero-lighting toggle and day.
            HeroLighting = g.GameEndConditions.HeroLighting,
            HeroLightingDay = g.GameEndConditions.HeroLightingDay,
            GladiatorArena = g.GladiatorArenaRules.Enabled,
            GladiatorArenaDaysDelayStart = g.GladiatorArenaRules.DaysDelayStart,
            GladiatorArenaCountDay = g.GladiatorArenaRules.CountDay,
            Tournament = g.TournamentRules.Enabled,
            TournamentFirstTournamentDay = g.TournamentRules.FirstTournamentDay,
            TournamentInterval = g.TournamentRules.Interval,
            TournamentPointsToWin = g.TournamentRules.PointsToWin,
            TournamentSaveArmy = g.TournamentRules.SaveArmy,
            // Master flag is now derived from the per-feature flags so older
            // readers (pre-T-302) still see the experimental nav when any
            // per-feature toggle is on. New per-feature flags below are the
            // source of truth.
            ExperimentalEnabled        = experimental.AnyEnabled,
            ExpFeatureGameMode         = experimental.GameMode,
            ExpFeatureStartingBonuses  = experimental.StartingBonuses,
            ExpFeatureZoneContent      = experimental.ZoneContent,
            ExpFeatureBordersRoads     = experimental.BordersRoads,
            ExpFeaturePerTierOverrides = experimental.PerTierOverrides,
            GameMode = g.GameMode,
            HeroHireBan = g.HeroHireBan,
            DesertionDay = g.DesertionDay,
            DesertionValue = g.DesertionValue,
            TerrainObstaclesFill = g.Terrain.ObstaclesFill,
            TerrainLakesFill = g.Terrain.LakesFill,
            BorderCornerRadius   = g.BordersRoads.CornerRadius,
            BorderObstaclesWidth = g.BordersRoads.ObstaclesWidth,
            WaterBorderEnabled   = g.BordersRoads.WaterBorderEnabled,
            WaterWidth           = g.BordersRoads.WaterWidth,
            RoadType             = g.BordersRoads.RoadType ?? "",
            ZoneDiplomacyModifier  = g.ZoneOverrides.DiplomacyModifier,
            ZoneCrossroadsPosition = g.ZoneOverrides.CrossroadsPosition,
            ZoneContentBiomeType   = g.ZoneOverrides.ContentBiomeType ?? "",
            ZoneContentBiomeArg    = g.ZoneOverrides.ContentBiomeArg ?? "",
            ZoneMetaObjectsBiomeType = g.ZoneOverrides.MetaObjectsBiomeType ?? "",
            ZoneMetaObjectsBiomeArg  = g.ZoneOverrides.MetaObjectsBiomeArg ?? "",
            ZoneGuardCutoffValue   = g.ZoneOverrides.GuardCutoffValue,
            ZoneGuardedContentPool = SidCsv.Join(g.ZoneOverrides.GuardedContentPool),
            ZoneUnguardedContentPool = SidCsv.Join(g.ZoneOverrides.UnguardedContentPool),
            ZoneContentCountLimits = SidCsv.Join(g.ZoneOverrides.ContentCountLimitRefs),
            ZoneGuardMultiplier    = g.ZoneOverrides.GuardMultiplier,
            ZoneGuardRandomization = g.ZoneOverrides.GuardRandomization,
            // T-503 — per-zone content/resource value overrides.
            ZoneResourcesValue                = g.ZoneOverrides.ResourcesValue,
            ZoneResourcesValuePerArea         = g.ZoneOverrides.ResourcesValuePerArea,
            ZoneGuardedContentValue           = g.ZoneOverrides.GuardedContentValue,
            ZoneGuardedContentValuePerArea    = g.ZoneOverrides.GuardedContentValuePerArea,
            ZoneUnguardedContentValue         = g.ZoneOverrides.UnguardedContentValue,
            ZoneUnguardedContentValuePerArea  = g.ZoneOverrides.UnguardedContentValuePerArea,
            EncounterHolesEnabled            = g.EncounterHoles.Enabled,
            EncounterHolesAffectedEncounters = g.EncounterHoles.AffectedEncounters,
            EncounterHolesTwoHoleEncounters  = g.EncounterHoles.TwoHoleEncounters,
            BuildingPresetPlayer = g.BuildingPresets.PlayerZonePreset,
            BuildingPresetNeutral = g.BuildingPresets.NeutralZonePreset,
            ZoneGuardWeeklyIncrement = g.GuardProgression.ZoneGuardWeeklyIncrement,
            ConnectionGuardWeeklyIncrement = g.GuardProgression.ConnectionGuardWeeklyIncrement,
            GuardReactionPreset = g.GuardReaction.Preset == GuardReactionPreset.Default
                ? ""
                : g.GuardReaction.Preset.ToString(),
            GuardReactionCustomDistribution = g.GuardReaction.CustomDistribution is { Count: > 0 }
                ? string.Join(",", g.GuardReaction.CustomDistribution)
                : "",
            ConnectionLength = g.ConnectionDefaults.Length,
            ConnectionGatePlacement = g.ConnectionDefaults.GatePlacement ?? "",
            ConnectionGuardEscape = g.ConnectionDefaults.GuardEscape,
            ConnectionSimTurnSquad = g.ConnectionDefaults.SimTurnSquad,
            ConnectionGuardRandomization = g.ConnectionDefaults.GuardRandomization,
            NeutralCityGuardChance = g.NeutralCities.GuardChance,
            NeutralCityGuardValuePercent = g.NeutralCities.GuardValuePercent,
            NeutralCityRemoveGuardIfHasOwner = g.NeutralCities.RemoveGuardIfHasOwner,
            GlobalBans = new List<string>(g.Content.GlobalBans),
            ContentCountLimits = g.Content.ContentCountLimits.ConvertAll(
                l => new ContentLimitFile { Sid = l.Sid, MaxPerPlayer = l.MaxPerPlayer }),
            ValueOverrides = g.Content.ValueOverrides.ConvertAll(
                v => new ValueOverrideFile { Sid = v.Sid, Variant = v.Variant, GuardValue = v.GuardValue }),
            BonusResources = new Dictionary<string,int>(g.Bonuses.Resources),
            BonusHeroAttack = g.Bonuses.HeroAttack,
            BonusHeroDefense = g.Bonuses.HeroDefense,
            BonusHeroSpellpower = g.Bonuses.HeroSpellpower,
            BonusHeroKnowledge = g.Bonuses.HeroKnowledge,
            BonusHeroStatStartHeroOnly = g.Bonuses.HeroStatStartHeroOnly,
            BonusItemSid = g.Bonuses.ItemSid,
            BonusItemStartHeroOnly = g.Bonuses.ItemStartHeroOnly,
            BonusSpellSid = g.Bonuses.SpellSid,
            BonusSpellStartHeroOnly = g.Bonuses.SpellStartHeroOnly,
            BonusUnitMultiplier = g.Bonuses.UnitMultiplier,
            BonusUnitMultiplierStartHeroOnly = g.Bonuses.UnitMultiplierStartHeroOnly,
            BonusPerPlayerOverrides = g.Bonuses.PerPlayerOverrides.ConvertAll(o => new PerPlayerBonusFile
            {
                PlayerSlot = o.PlayerSlot,
                Resources = new Dictionary<string,int>(o.Bonuses.Resources),
                HeroAttack = o.Bonuses.HeroAttack,
                HeroDefense = o.Bonuses.HeroDefense,
                HeroSpellpower = o.Bonuses.HeroSpellpower,
                HeroKnowledge = o.Bonuses.HeroKnowledge,
                HeroStatStartHeroOnly = o.Bonuses.HeroStatStartHeroOnly,
                ItemSid = o.Bonuses.ItemSid,
                ItemStartHeroOnly = o.Bonuses.ItemStartHeroOnly,
                SpellSid = o.Bonuses.SpellSid,
                SpellStartHeroOnly = o.Bonuses.SpellStartHeroOnly,
                UnitMultiplier = o.Bonuses.UnitMultiplier,
                UnitMultiplierStartHeroOnly = o.Bonuses.UnitMultiplierStartHeroOnly,
            }),
            TierLow = TierToFile(a.LowTier),
            TierMedium = TierToFile(a.MediumTier),
            TierHigh = TierToFile(a.HighTier),
        };

        // Reference-shared: nested DTO trees are owned by the deserialized side after assignment.
        // Round 4 UI must clone before mutating if it cares about file/in-memory aliasing.
        file.PlayerZoneContent   = g.PlayerZoneContent;
        file.NeutralZoneContent  = g.NeutralZoneContent;
        file.ZoneRoadDecorations = g.ZoneRoadDecorations;
        return file;
    }

    private static GuardReactionPreset ParseGuardReactionPreset(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return GuardReactionPreset.Default;
        return Enum.TryParse<GuardReactionPreset>(raw, ignoreCase: true, out var v)
            ? v
            : GuardReactionPreset.Default;
    }

    private static List<int> ParseDistributionCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new();
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<int>(parts.Length);
        foreach (var p in parts)
        {
            if (int.TryParse(p.Trim(), out int v) && v >= 0)
                result.Add(v);
            else
                return new(); // any malformed token → discard whole list, fall through to default.
        }
        return result;
    }

    private static TierOverrides TierFromFile(TierOverrideFile? f) =>
        f is null ? new TierOverrides() : new TierOverrides
        {
            ObstaclesFill = f.ObstaclesFill,
            LakesFill = f.LakesFill,
            BuildingPreset = f.BuildingPreset ?? "",
            GuardWeeklyIncrement = f.GuardWeeklyIncrement,
        };

    private static TierOverrideFile TierToFile(TierOverrides t) => new()
    {
        ObstaclesFill = t.ObstaclesFill,
        LakesFill = t.LakesFill,
        BuildingPreset = t.BuildingPreset,
        GuardWeeklyIncrement = t.GuardWeeklyIncrement,
    };
}
