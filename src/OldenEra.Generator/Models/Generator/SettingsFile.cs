using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Models
{
    public sealed class ContentLimitFile
    {
        [JsonPropertyName("sid")] public string Sid { get; set; } = "";
        [JsonPropertyName("maxPerPlayer")] public int MaxPerPlayer { get; set; } = 1;
    }

    /// <summary>Persisted shape for one <see cref="ValueOverrideSetting"/> row. T-003.</summary>
    public sealed class ValueOverrideFile
    {
        [JsonPropertyName("sid")] public string Sid { get; set; } = "";
        [JsonPropertyName("variant")] public int Variant { get; set; } = -1;
        [JsonPropertyName("guardValue")] public int GuardValue { get; set; } = 0;
    }

    public sealed class TierOverrideFile
    {
        [JsonPropertyName("obstaclesFill")] public double ObstaclesFill { get; set; } = 0.0;
        [JsonPropertyName("lakesFill")] public double LakesFill { get; set; } = 0.0;
        [JsonPropertyName("buildingPreset")] public string BuildingPreset { get; set; } = "";
        [JsonPropertyName("guardWeeklyIncrement")] public double GuardWeeklyIncrement { get; set; } = 0.0;
    }

    /// <summary>
    /// Persisted shape for one per-player bonus override row. Mirrors the uniform
    /// bonus field surface so adding a new field is a one-place change. T-206.
    /// </summary>
    public sealed class PerPlayerBonusFile
    {
        [JsonPropertyName("playerSlot")] public int PlayerSlot { get; set; } = 1;

        [JsonPropertyName("resources")]              public Dictionary<string,int> Resources { get; set; } = new();
        [JsonPropertyName("heroAttack")]             public int HeroAttack { get; set; } = 0;
        [JsonPropertyName("heroDefense")]            public int HeroDefense { get; set; } = 0;
        [JsonPropertyName("heroSpellpower")]         public int HeroSpellpower { get; set; } = 0;
        [JsonPropertyName("heroKnowledge")]          public int HeroKnowledge { get; set; } = 0;
        [JsonPropertyName("heroStatStartHeroOnly")]  public bool HeroStatStartHeroOnly { get; set; } = false;
        [JsonPropertyName("itemSid")]                public string ItemSid { get; set; } = "";
        [JsonPropertyName("itemStartHeroOnly")]      public bool ItemStartHeroOnly { get; set; } = false;
        [JsonPropertyName("spellSid")]               public string SpellSid { get; set; } = "";
        [JsonPropertyName("spellStartHeroOnly")]     public bool SpellStartHeroOnly { get; set; } = false;
        [JsonPropertyName("unitMultiplier")]         public double UnitMultiplier { get; set; } = 0.0;
        [JsonPropertyName("unitMultiplierStartHeroOnly")] public bool UnitMultiplierStartHeroOnly { get; set; } = false;
    }

    /// <summary>
    /// Persisted settings file (.oetgs) — all user-configurable UI state.
    /// </summary>
    public sealed class SettingsFile
    {
        [JsonPropertyName("templateName")]      public string  TemplateName           { get; set; } = "Custom Template";
        [JsonPropertyName("seed")]              public int?    Seed                   { get; set; } = null;
        [JsonPropertyName("mapSize")]           public int     MapSize                { get; set; } = 160;
        [JsonPropertyName("playerCount")]       public int     PlayerCount            { get; set; } = 2;
        [JsonPropertyName("neutralZoneCount")]  public int     NeutralZoneCount       { get; set; } = 0;
        [JsonPropertyName("playerCastles")]     public int     PlayerZoneCastles      { get; set; } = 1;
        [JsonPropertyName("neutralCastles")]    public int     NeutralZoneCastles     { get; set; } = 1;
        [JsonPropertyName("advancedMode")]      public bool    AdvancedMode           { get; set; } = false;
        [JsonPropertyName("neutralLowNoCastle")]    public int NeutralLowNoCastleCount    { get; set; } = 0;
        [JsonPropertyName("neutralLowCastle")]      public int NeutralLowCastleCount      { get; set; } = 0;
        [JsonPropertyName("neutralMediumNoCastle")] public int NeutralMediumNoCastleCount { get; set; } = 0;
        [JsonPropertyName("neutralMediumCastle")]   public int NeutralMediumCastleCount   { get; set; } = 0;
        [JsonPropertyName("neutralHighNoCastle")]   public int NeutralHighNoCastleCount   { get; set; } = 0;
        [JsonPropertyName("neutralHighCastle")]     public int NeutralHighCastleCount     { get; set; } = 0;
        [JsonPropertyName("matchPlayerCastleFactions")] public bool MatchPlayerCastleFactions { get; set; } = false;
        [JsonPropertyName("minNeutralZonesBetweenPlayers")] public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        [JsonPropertyName("experimentalBalancedZonePlacement")] public bool ExperimentalBalancedZonePlacement { get; set; } = false; // legacy: migrated to Topology=Balanced on load
        [JsonPropertyName("experimentalMapSizes")] public bool ExperimentalMapSizes { get; set; } = false;
        [JsonPropertyName("playerZoneSize")]  public double  PlayerZoneSize       { get; set; } = 1.0;
        [JsonPropertyName("neutralZoneSize")] public double  NeutralZoneSize      { get; set; } = 1.0;
        [JsonPropertyName("hubZoneSize")]     public double  HubZoneSize          { get; set; } = 1.0;
        [JsonPropertyName("hubCastles")]      public int     HubZoneCastles       { get; set; } = 0;
        [JsonPropertyName("guardRandomization")] public double GuardRandomization { get; set; } = 0.05;
        [JsonPropertyName("heroMin")]           public int     HeroCountMin           { get; set; } = 4;
        [JsonPropertyName("heroMax")]           public int     HeroCountMax           { get; set; } = 8;
        [JsonPropertyName("heroIncrement")]     public int     HeroCountIncrement     { get; set; } = 1;
        [JsonPropertyName("heroBans")]          public List<string> HeroBans          { get; set; } = new();
        [JsonPropertyName("bannedSpells")]      public List<string> BannedSpells      { get; set; } = new();
        [JsonPropertyName("fixedStartingHeroByFaction")] public Dictionary<string, string?> FixedStartingHeroByFaction { get; set; } = new();
        [JsonPropertyName("topology")]          public MapTopology Topology           { get; set; } = MapTopology.Random;
        [JsonPropertyName("randomPortals")]     public bool    RandomPortals          { get; set; } = false;
        [JsonPropertyName("maxPortalConns")]    public int     MaxPortalConnections   { get; set; } = 32;
        [JsonPropertyName("spawnFootholds")]    public bool    SpawnRemoteFootholds   { get; set; } = true;
        [JsonPropertyName("generateRoads")]     public bool    GenerateRoads          { get; set; } = true;
        [JsonPropertyName("isolateplayers")]    public bool    NoDirectPlayerConn     { get; set; } = false;
        [JsonPropertyName("resourceDensity")]   public int?    ResourceDensityPercent       { get; set; }
        [JsonPropertyName("structureDensity")]  public int?    StructureDensityPercent      { get; set; }
        [JsonPropertyName("neutralStackStrength")] public int  NeutralStackStrengthPercent  { get; set; } = 100;
        [JsonPropertyName("borderGuardStrength")]  public int  BorderGuardStrengthPercent   { get; set; } = 100;
        [JsonPropertyName("victoryCondition")]  public string  VictoryCondition             { get; set; } = "win_condition_1";
        [JsonPropertyName("factionLawsExp")]    public int     FactionLawsExpPercent        { get; set; } = 100;
        [JsonPropertyName("astrologyExp")]      public int     AstrologyExpPercent          { get; set; } = 100;
        [JsonPropertyName("lostStartCity")]     public bool    LostStartCity                { get; set; } = false;
        [JsonPropertyName("lostStartCityDay")]  public int     LostStartCityDay             { get; set; } = 3;
        [JsonPropertyName("lostStartHero")]     public bool    LostStartHero                { get; set; } = false;
        [JsonPropertyName("cityHold")]          public bool    CityHold                     { get; set; } = false;
        [JsonPropertyName("cityHoldDays")]      public int     CityHoldDays                 { get; set; } = 6;
        [JsonPropertyName("gladiatorArena")]    public bool    GladiatorArena               { get; set; } = false;
        [JsonPropertyName("gladiatorArenaDaysDelayStart")] public int GladiatorArenaDaysDelayStart { get; set; } = 30;
        [JsonPropertyName("gladiatorArenaCountDay")] public int GladiatorArenaCountDay       { get; set; } = 3;
        [JsonPropertyName("tournament")]        public bool    Tournament                   { get; set; } = false;
        [JsonPropertyName("tournamentFirstTournamentDay")] public int TournamentFirstTournamentDay { get; set; } = 14;
        [JsonPropertyName("tournamentInterval")] public int TournamentInterval    { get; set; } = 7;
        [JsonPropertyName("tournamentPointsToWin")] public int TournamentPointsToWin        { get; set; } = 2;
        [JsonPropertyName("tournamentSaveArmy")] public bool TournamentSaveArmy             { get; set; } = true;

        // ── Experimental ────────────────────────────────────────────────────────
        /// <summary>
        /// Legacy master toggle. Pre-T-302 .oetgs files persisted only this
        /// flag; on load (see <see cref="OldenEra.Generator.Services.SettingsMapper.FromFile"/>)
        /// a true value with all per-feature flags at default migrates to all
        /// per-feature flags = true. Still written on save for older clients
        /// — set to true when any per-feature flag is on so old readers still
        /// see the experimental nav.
        /// </summary>
        [JsonPropertyName("experimentalEnabled")] public bool  ExperimentalEnabled          { get; set; } = false;

        // T-302 — per-feature flags. Each gates one experimental section card.
        // All are share-codec-safe (plain bool).
        [JsonPropertyName("expFeatureGameMode")]          public bool ExpFeatureGameMode          { get; set; } = false;
        [JsonPropertyName("expFeatureStartingBonuses")]   public bool ExpFeatureStartingBonuses   { get; set; } = false;
        [JsonPropertyName("expFeatureZoneContent")]       public bool ExpFeatureZoneContent       { get; set; } = false;
        [JsonPropertyName("expFeatureBordersRoads")]      public bool ExpFeatureBordersRoads      { get; set; } = false;
        [JsonPropertyName("expFeaturePerTierOverrides")]  public bool ExpFeaturePerTierOverrides  { get; set; } = false;
        [JsonPropertyName("gameMode")]          public string  GameMode                     { get; set; } = "Classic";
        [JsonPropertyName("heroHireBan")]       public bool    HeroHireBan                  { get; set; } = false;
        [JsonPropertyName("desertionDay")]      public int     DesertionDay                 { get; set; } = 0;
        [JsonPropertyName("desertionValue")]    public int     DesertionValue               { get; set; } = 0;

        [JsonPropertyName("terrainObstaclesFill")] public double TerrainObstaclesFill       { get; set; } = 0.0;
        [JsonPropertyName("terrainLakesFill")]     public double TerrainLakesFill           { get; set; } = 0.0;

        [JsonPropertyName("borderCornerRadius")]   public double? BorderCornerRadius   { get; set; }
        [JsonPropertyName("borderObstaclesWidth")] public int?    BorderObstaclesWidth { get; set; }
        [JsonPropertyName("waterBorderEnabled")]   public bool    WaterBorderEnabled   { get; set; } = false;
        [JsonPropertyName("waterWidth")]           public int     WaterWidth           { get; set; } = 4;
        [JsonPropertyName("roadType")]             public string  RoadType             { get; set; } = "";

        // Per-zone overrides (T-005). null / "" = generator default; emit nothing.
        // Round-trips through SettingsShareCodec because all fields are scalar / nullable / string.
        [JsonPropertyName("zoneDiplomacyModifier")]   public double? ZoneDiplomacyModifier   { get; set; }
        [JsonPropertyName("zoneCrossroadsPosition")]  public int?    ZoneCrossroadsPosition  { get; set; }
        [JsonPropertyName("zoneContentBiomeType")]    public string  ZoneContentBiomeType    { get; set; } = "";
        [JsonPropertyName("zoneContentBiomeArg")]     public string  ZoneContentBiomeArg     { get; set; } = "";
        // T-203: metaObjectsBiome selector (mirrors contentBiome shape).
        [JsonPropertyName("zoneMetaObjectsBiomeType")] public string ZoneMetaObjectsBiomeType { get; set; } = "";
        [JsonPropertyName("zoneMetaObjectsBiomeArg")]  public string ZoneMetaObjectsBiomeArg  { get; set; } = "";

        // Per-zone overrides (T-006). null / "" = generator default; emit nothing.
        // Lists are stored as CSV strings so the share codec's value-equality
        // "non-default" check (see SettingsShareCodec.CopyNonDefault) keeps working —
        // adding List<string> here would silently break per-field recovery.
        [JsonPropertyName("zoneGuardCutoffValue")]    public int?    ZoneGuardCutoffValue    { get; set; }
        [JsonPropertyName("zoneGuardedContentPool")]  public string  ZoneGuardedContentPool  { get; set; } = "";
        [JsonPropertyName("zoneUnguardedContentPool")] public string ZoneUnguardedContentPool { get; set; } = "";
        [JsonPropertyName("zoneContentCountLimits")] public string  ZoneContentCountLimits  { get; set; } = "";
        // T-502 — per-template overrides for the per-zone guard scalars. null = unset.
        [JsonPropertyName("zoneGuardMultiplier")]    public double? ZoneGuardMultiplier    { get; set; }
        [JsonPropertyName("zoneGuardRandomization")] public double? ZoneGuardRandomization { get; set; }

        // T-201 — encounter-holes (multi-stack battles). Disabled by default;
        // existing snapshots stay byte-identical. When enabled, GameRules.encounterHoles
        // flips to true and Zone.encounterHolesSettings is stamped uniformly.
        // Three scalar fields → round-trips cleanly through SettingsShareCodec's
        // value-equality "non-default" comparison (see CopyNonDefault).
        [JsonPropertyName("encounterHolesEnabled")]            public bool   EncounterHolesEnabled            { get; set; } = false;
        [JsonPropertyName("encounterHolesAffectedEncounters")] public double EncounterHolesAffectedEncounters { get; set; } = 0.66;
        [JsonPropertyName("encounterHolesTwoHoleEncounters")]  public double EncounterHolesTwoHoleEncounters  { get; set; } = 0.66;

        [JsonPropertyName("buildingPresetPlayer")]  public string BuildingPresetPlayer      { get; set; } = "";
        [JsonPropertyName("buildingPresetNeutral")] public string BuildingPresetNeutral     { get; set; } = "";

        [JsonPropertyName("zoneGuardWeeklyIncrement")]       public double ZoneGuardWeeklyIncrement       { get; set; } = 0.0;
        [JsonPropertyName("connectionGuardWeeklyIncrement")] public double ConnectionGuardWeeklyIncrement { get; set; } = 0.0;

        /// <summary>
        /// Preset selector for the per-zone guardReactionDistribution curve.
        /// Stored as a string so SettingsShareCodec's value-type comparison
        /// stays correct. "" / "Default" = leave generator defaults alone.
        /// </summary>
        [JsonPropertyName("guardReactionPreset")] public string GuardReactionPreset { get; set; } = "";

        /// <summary>
        /// Custom distribution as a comma-separated CSV of six non-negative ints
        /// (e.g. <c>"0,10,10,10,10,0"</c>). Empty string = no custom override.
        /// Only consulted when <see cref="GuardReactionPreset"/> = "Custom".
        /// String (not List&lt;int&gt;) to keep SettingsShareCodec's reflection
        /// "non-default" comparison sound.
        /// </summary>
        [JsonPropertyName("guardReactionCustomDistribution")] public string GuardReactionCustomDistribution { get; set; } = "";

        // T-001 — connection-level scalar defaults applied uniformly to every
        // emitted Connection. Sentinel values (0 / "" / null) mean "unset" and
        // round-trip without altering generator output.
        [JsonPropertyName("connectionLength")]        public double ConnectionLength       { get; set; } = 0.0;
        [JsonPropertyName("connectionGatePlacement")] public string ConnectionGatePlacement { get; set; } = "";
        [JsonPropertyName("connectionGuardEscape")]   public bool?  ConnectionGuardEscape   { get; set; }
        [JsonPropertyName("connectionSimTurnSquad")]  public bool?  ConnectionSimTurnSquad  { get; set; }
        // T-501 — per-template default for Connection.guardRandomization. null = unset.
        [JsonPropertyName("connectionGuardRandomization")] public double? ConnectionGuardRandomization { get; set; }

        [JsonPropertyName("neutralCityGuardChance")]        public double NeutralCityGuardChance        { get; set; } = 0.0;
        [JsonPropertyName("neutralCityGuardValuePercent")]  public int    NeutralCityGuardValuePercent  { get; set; } = 100;
        [JsonPropertyName("neutralCityRemoveGuardIfHasOwner")] public bool NeutralCityRemoveGuardIfHasOwner { get; set; } = false;

        [JsonPropertyName("globalBans")]            public List<string> GlobalBans              { get; set; } = new();
        [JsonPropertyName("contentCountLimits")]    public List<ContentLimitFile> ContentCountLimits = new();
        [JsonPropertyName("valueOverrides")]        public List<ValueOverrideFile> ValueOverrides { get; set; } = new();

        [JsonPropertyName("bonusResources")]        public Dictionary<string,int> BonusResources = new();
        [JsonPropertyName("bonusHeroAttack")]       public int BonusHeroAttack       { get; set; } = 0;
        [JsonPropertyName("bonusHeroDefense")]      public int BonusHeroDefense      { get; set; } = 0;
        [JsonPropertyName("bonusHeroSpellpower")]   public int BonusHeroSpellpower   { get; set; } = 0;
        [JsonPropertyName("bonusHeroKnowledge")]    public int BonusHeroKnowledge    { get; set; } = 0;
        [JsonPropertyName("bonusHeroStatStartHeroOnly")] public bool BonusHeroStatStartHeroOnly { get; set; } = false;
        [JsonPropertyName("bonusItemSid")]          public string BonusItemSid       { get; set; } = "";
        [JsonPropertyName("bonusItemStartHeroOnly")] public bool BonusItemStartHeroOnly { get; set; } = false;
        [JsonPropertyName("bonusSpellSid")]         public string BonusSpellSid      { get; set; } = "";
        [JsonPropertyName("bonusSpellStartHeroOnly")] public bool BonusSpellStartHeroOnly { get; set; } = false;
        [JsonPropertyName("bonusUnitMultiplier")]   public double BonusUnitMultiplier { get; set; } = 0.0;
        [JsonPropertyName("bonusUnitMultiplierStartHeroOnly")] public bool BonusUnitMultiplierStartHeroOnly { get; set; } = false;
        /// <summary>Per-player bonus overrides. Empty list emits no per-slot rows. T-206.</summary>
        [JsonPropertyName("bonusPerPlayerOverrides")] public List<PerPlayerBonusFile> BonusPerPlayerOverrides { get; set; } = new();

        [JsonPropertyName("tierLow")]    public TierOverrideFile TierLow    { get; set; } = new();
        [JsonPropertyName("tierMedium")] public TierOverrideFile TierMedium { get; set; } = new();
        [JsonPropertyName("tierHigh")]   public TierOverrideFile TierHigh   { get; set; } = new();

        // Legacy setting from v0.2 and earlier; when present, it seeds both split density sliders.
        [JsonPropertyName("contentDensity")]    public int?    ContentDensityPercent        { get; set; }

        [JsonIgnore] public int EffectiveResourceDensityPercent  => ResourceDensityPercent  ?? ContentDensityPercent ?? 100;
        [JsonIgnore] public int EffectiveStructureDensityPercent => StructureDensityPercent ?? ContentDensityPercent ?? 100;

        [JsonPropertyName("playerZoneContent")]
        public ZoneContentList PlayerZoneContent { get; set; } = new();

        [JsonPropertyName("neutralZoneContent")]
        public NeutralZoneContent NeutralZoneContent { get; set; } = new();

        [JsonPropertyName("zoneRoadDecorations")]
        public List<ZoneRoadDecoration> ZoneRoadDecorations { get; set; } = new();
    }
}
