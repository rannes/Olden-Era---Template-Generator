using System.Collections.Generic;

namespace OldenEra.Generator.Models
{
    public class TournamentRules
    {
        public bool Enabled { get; set; } = false;
        public int FirstTournamentDay { get; set; } = 14;
        public int Interval { get; set; } = 7;
        public int PointsToWin { get; set; } = 2;
        public bool SaveArmy { get; set; } = true;
    }
    public class GladiatorArenaRules
    {
        public bool Enabled { get; set; } = false;
        public int DaysDelayStart { get; set; } = 30;
        public int CountDay { get; set; } = 3;
    }

    public class GameEndConditions
    {
        public string VictoryCondition { get; set; } = "win_condition_1";
        public bool LostStartCity { get; set; } = false;
        public int LostStartCityDay { get; set; } = 3;
        public bool LostStartHero { get; set; } = false;
        public bool CityHold { get; set; } = false;
        public int CityHoldDays { get; set; } = 6;

        /// <summary>
        /// Hero-lighting (resurrection) victory rule. Default <c>true</c> matches
        /// every shipped preset, which emit <c>heroLighting: true, heroLightingDay: 1</c>
        /// on <c>winConditions</c>. Set to <c>false</c> to omit both fields entirely.
        /// T-506.
        /// </summary>
        public bool HeroLighting { get; set; } = true;

        /// <summary>
        /// Day on which the hero-lighting rule activates. Only emitted when
        /// <see cref="HeroLighting"/> is <c>true</c>. Clamped to 1..30 on emit.
        /// Default <c>1</c> matches every shipped preset. T-506.
        /// </summary>
        public int HeroLightingDay { get; set; } = 1;
    }

    public class HeroSettings
    {
        public int HeroCountMin { get; set; } = 4;
        public int HeroCountMax { get; set; } = 8;
        public int HeroCountIncrement { get; set; } = 1;

        /// <summary>
        /// Hero IDs (e.g. <c>"human_hero_3"</c>) banned from the template.
        /// Emitted into <c>globalBans.heroes</c>; matches the schema seen in
        /// shipped templates (Arcade.rmg.json).
        /// </summary>
        public List<string> HeroBans { get; set; } = new();

        /// <summary>
        /// Spell IDs (e.g. <c>"spell.fly"</c>) banned from the template.
        /// Emitted into <c>globalBans.magics</c>; sibling of <see cref="HeroBans"/>.
        /// </summary>
        public List<string> BannedSpells { get; set; } = new();
    }

    public class AdvancedSettings
    {
        public bool Enabled { get; set; } = false;
        public int NeutralLowNoCastleCount { get; set; } = 0;
        public int NeutralLowCastleCount { get; set; } = 0;
        public int NeutralMediumNoCastleCount { get; set; } = 0;
        public int NeutralMediumCastleCount { get; set; } = 0;
        public int NeutralHighNoCastleCount { get; set; } = 0;
        public int NeutralHighCastleCount { get; set; } = 0;
        public double PlayerZoneSize { get; set; } = 1.0;
        public double NeutralZoneSize { get; set; } = 1.0;
        public double GuardRandomization { get; set; } = 0.05;

        // Per-tier experimental overrides. Empty / 0 = fall through to the global value.
        public TierOverrides LowTier { get; set; } = new TierOverrides();
        public TierOverrides MediumTier { get; set; } = new TierOverrides();
        public TierOverrides HighTier { get; set; } = new TierOverrides();
    }

    /// <summary>
    /// Per-neutral-tier overrides that win over the global Terrain / BuildingPresets /
    /// GuardProgression values when set. All defaults are no-op.
    /// </summary>
    public class TierOverrides
    {
        public double ObstaclesFill { get; set; } = 0.0;
        public double LakesFill { get; set; } = 0.0;
        public string BuildingPreset { get; set; } = "";
        public double GuardWeeklyIncrement { get; set; } = 0.0;
    }
    public class ZoneConfiguration
    {
        public int NeutralZoneCount { get; set; } = 0;
        public int PlayerZoneCastles { get; set; } = 1;
        public int NeutralZoneCastles { get; set; } = 1;
        public int ResourceDensityPercent { get; set; } = 100;
        public int StructureDensityPercent { get; set; } = 100;
        public int NeutralStackStrengthPercent { get; set; } = 100;
        public int BorderGuardStrengthPercent { get; set; } = 100;
        public double HubZoneSize { get; set; } = 1.0;
        public int HubZoneCastles { get; set; } = 0;
        public AdvancedSettings Advanced { get; set; } = new AdvancedSettings();
    }

    // ── Experimental capabilities ───────────────────────────────────────────────
    // All fields default to "unset" and are no-ops in the generator output. The
    // generator only emits non-default values, so existing fixtures stay byte-
    // identical until a user changes something.

    public class TerrainSettings
    {
        /// <summary>0 = unset, otherwise overrides the per-zone obstaclesFill (0..1).</summary>
        public double ObstaclesFill { get; set; } = 0.0;
        /// <summary>0 = unset, otherwise overrides the per-zone lakesFill (0..1).</summary>
        public double LakesFill { get; set; } = 0.0;
    }

    public class BuildingPresetSettings
    {
        /// <summary>Empty = use generator default. Otherwise a value from KnownValues.BuildingsConstructionSids.</summary>
        public string PlayerZonePreset { get; set; } = "";
        public string NeutralZonePreset { get; set; } = "";
    }

    public class GuardProgressionSettings
    {
        /// <summary>0 = use generator default per zone. Otherwise the value to stamp on every zone.</summary>
        public double ZoneGuardWeeklyIncrement { get; set; } = 0.0;
        /// <summary>0 = use generator default per connection. Otherwise the value to stamp on every connection.</summary>
        public double ConnectionGuardWeeklyIncrement { get; set; } = 0.0;
    }

    /// <summary>
    /// Preset shape for <see cref="Zone.GuardReactionDistribution"/>. The array is
    /// six weekly buckets (week 0..week 5). Higher values relative to the others
    /// mean guards are more likely to spawn that week.
    /// </summary>
    public enum GuardReactionPreset
    {
        /// <summary>Use the generator's per-zone defaults. Existing snapshots stay byte-identical.</summary>
        Default = 0,
        /// <summary>Most guards on day 1; tapers off. Like Spawn zones today: [60,20,10,10,2,0].</summary>
        FrontLoaded,
        /// <summary>Roughly uniform across weeks: [10,10,10,10,10,10].</summary>
        Even,
        /// <summary>Light early, heavy late: [0,2,10,20,30,40].</summary>
        BackLoaded,
        /// <summary>Use <see cref="GuardReactionSettings.CustomDistribution"/> verbatim.</summary>
        Custom,
    }

    public class GuardReactionSettings
    {
        /// <summary>
        /// Curve preset applied to every zone the generator emits. Default = leave
        /// each zone's per-type baked-in array untouched.
        /// </summary>
        public GuardReactionPreset Preset { get; set; } = GuardReactionPreset.Default;

        /// <summary>
        /// Six non-negative weekly weights (week 0..week 5). Only consulted when
        /// <see cref="Preset"/> is <see cref="GuardReactionPreset.Custom"/>. Empty
        /// = falls back to the default behavior.
        /// </summary>
        public List<int> CustomDistribution { get; set; } = new();
    }

    public class NeutralCitySettings
    {
        /// <summary>0 = use generator default. Otherwise the chance any neutral city is guarded (0..1).</summary>
        public double GuardChance { get; set; } = 0.0;
        /// <summary>100 = unmodified. Otherwise scales the per-city guardValue.</summary>
        public int GuardValuePercent { get; set; } = 100;
        /// <summary>When true, neutral cities drop their guard once captured (emits removeGuardIfHasOwner: true).</summary>
        public bool RemoveGuardIfHasOwner { get; set; } = false;
    }

    public class ContentLimit
    {
        public string Sid { get; set; } = "";
        public int MaxPerPlayer { get; set; } = 1;
    }

    /// <summary>
    /// One row of <c>RmgTemplate.valueOverrides</c>. Mirrors the schema seen in
    /// shipped templates (e.g. Anarchy.rmg.json):
    /// <c>{ "sid": "...", "variant": -1, "guardValue": 6000 }</c>.
    /// <c>Variant</c> defaults to -1 ("any variant"); <c>GuardValue</c> is the
    /// only currently meaningful override and must be > 0 to be emitted.
    /// </summary>
    public class ValueOverrideSetting
    {
        public string Sid { get; set; } = "";
        /// <summary>-1 = any variant. Other non-negative ints pin a specific variant index.</summary>
        public int Variant { get; set; } = -1;
        /// <summary>0 = unset (row is skipped on emit). Otherwise the per-stack guard value.</summary>
        public int GuardValue { get; set; } = 0;
    }

    public class ContentControlSettings
    {
        /// <summary>SIDs the engine should ban globally.</summary>
        public List<string> GlobalBans { get; set; } = new();
        /// <summary>Extra count caps appended to the generator's defaults.</summary>
        public List<ContentLimit> ContentCountLimits { get; set; } = new();
        /// <summary>
        /// Per-SID guard-value overrides emitted as <c>valueOverrides</c> on the
        /// template. Empty list → field omitted. T-003.
        /// </summary>
        public List<ValueOverrideSetting> ValueOverrides { get; set; } = new();
    }

    public class StartingBonusSettings
    {
        // Bonus resources, keyed by Olden Era resource code (e.g. "gold", "wood").
        public Dictionary<string, int> Resources { get; set; } = new();

        // Hero stat bonuses. 0 = unset.
        public int HeroAttack { get; set; } = 0;
        public int HeroDefense { get; set; } = 0;
        public int HeroSpellpower { get; set; } = 0;
        public int HeroKnowledge { get; set; } = 0;
        public bool HeroStatStartHeroOnly { get; set; } = false;

        public string ItemSid { get; set; } = "";
        public bool ItemStartHeroOnly { get; set; } = false;

        public string SpellSid { get; set; } = "";
        public bool SpellStartHeroOnly { get; set; } = false;

        /// <summary>0 = unset. Otherwise a multiplier applied to starting unit counts.</summary>
        public double UnitMultiplier { get; set; } = 0.0;
        public bool UnitMultiplierStartHeroOnly { get; set; } = false;

        /// <summary>
        /// Per-player overrides layered on top of the uniform fields above. Each row
        /// targets one player slot (1..PlayerCount) and replaces only the fields it
        /// sets; unset fields inherit the uniform value. Empty list = uniform behavior
        /// unchanged. Duplicate slots: last-write-wins (validator warns). T-206.
        /// </summary>
        public List<PerPlayerBonusOverride> PerPlayerOverrides { get; set; } = new();
    }

    /// <summary>
    /// One row of <see cref="StartingBonusSettings.PerPlayerOverrides"/>. Reuses
    /// <see cref="StartingBonusSettings"/> as the field carrier so new bonus fields
    /// added later flow through automatically. The nested
    /// <see cref="StartingBonusSettings.PerPlayerOverrides"/> on <see cref="Bonuses"/>
    /// is unused and ignored. T-206.
    /// </summary>
    public class PerPlayerBonusOverride
    {
        /// <summary>1-based player slot. Out-of-range values are skipped on emit (validator warns).</summary>
        public int PlayerSlot { get; set; } = 1;

        /// <summary>Bonus fields for this slot. Sentinel-default fields inherit uniform.</summary>
        public StartingBonusSettings Bonuses { get; set; } = new();
    }

    /// Per-connection defaults applied uniformly to every <see cref="Connection"/>
    /// after topology builders run. Each field has an "unset" sentinel that maps
    /// to no emission so default-settings output stays byte-identical to current.
    /// </summary>
    /// <remarks>
    /// T-001 surfaces the four scalar Connection fields the game schema accepts
    /// (length, gatePlacement, guardEscape, simTurnSquad). The complex
    /// portalPlacementRulesFrom/To list-of-objects fields stay generator-only:
    /// their semantics depend on per-connection identity (target zone names,
    /// crossroads weights) and don't fit a "blanket default" model.
    /// </remarks>
    public class ConnectionDefaultsSettings
    {
        /// <summary>0 = unset. Non-zero stamps onto every connection's <c>length</c>.</summary>
        public double Length { get; set; } = 0.0;
        /// <summary>Empty = unset. Otherwise overlaid onto every connection's <c>gatePlacement</c>. Common value in shipped templates is "Center".</summary>
        public string GatePlacement { get; set; } = "";
        /// <summary>null = leave whatever the topology builder chose. Otherwise force this value on every connection.</summary>
        public bool? GuardEscape { get; set; }
        /// <summary>null = leave whatever the topology builder chose. Otherwise force this value on every connection.</summary>
        public bool? SimTurnSquad { get; set; }
        /// <summary>
        /// null = unset (field omitted from emitted JSON). Otherwise stamps a per-template
        /// default onto every connection's <c>guardRandomization</c>. Shipped templates
        /// use values like 0.10–0.15 (e.g. <c>All Around.rmg.json</c>); 0.0 is a valid
        /// "no randomization" override distinct from "unset".
        /// </summary>
        public double? GuardRandomization { get; set; }
    }

    /// <summary>
    /// Per-zone schema knobs the game accepts but the generator currently bakes
    /// to a single hardcoded value (see <c>BuildSpawnZone</c>, <c>BuildNeutralZone</c>,
    /// <c>BuildHubZone</c>). Each property is "auto" by default and, when set,
    /// is stamped onto every emitted zone by the experimental post-processor.
    ///
    /// <para>This is the seam T-006 will extend: per-zone overrides for content
    /// caps, <c>guardCutoffValue</c>, and content-pool selection share the same
    /// settings surface and the same UI panel.</para>
    /// </summary>
    public class ZoneOverridesSettings
    {
        /// <summary>
        /// Replaces <c>diplomacyModifier</c> on every zone. <c>null</c> = generator
        /// default (-0.5 today; matches every shipped example template).
        /// </summary>
        public double? DiplomacyModifier { get; set; }

        /// <summary>
        /// Replaces <c>crossroadsPosition</c> on every zone. Valid range 0..3
        /// (only 0 and 1 appear in shipped templates today). <c>null</c> = generator
        /// default (0 today).
        /// </summary>
        public int? CrossroadsPosition { get; set; }

        /// <summary>
        /// Override <c>contentBiome</c> selector type on every zone. Empty / null
        /// = generator default (zone-aware <c>MatchMainObject</c> / <c>MatchZone</c>).
        /// Valid values: "MatchMainObject", "MatchZone", "FromList".
        /// </summary>
        public string ContentBiomeType { get; set; } = "";

        /// <summary>
        /// Single argument forwarded into the override <c>contentBiome.args</c>.
        /// Empty = no args (matches the <c>FromList []</c> idiom in shipped templates).
        /// For <c>MatchMainObject</c> this is the main-object index ("0", "1");
        /// for <c>FromList</c> this is a biome name ("Sand", "Deathland", …).
        /// Ignored when <see cref="ContentBiomeType"/> is empty / "MatchZone".
        /// </summary>
        public string ContentBiomeArg { get; set; } = "";

        // ── T-203: metaObjectsBiome selector (mirrors contentBiome shape) ──

        /// <summary>
        /// Override <c>metaObjectsBiome</c> selector type on every zone. Empty / null
        /// = generator default (zones today omit the field, letting the engine pick;
        /// shipped templates use <c>MatchMainObject</c> "0" most commonly).
        /// Valid values: "MatchMainObject", "MatchZone", "FromList".
        /// </summary>
        public string MetaObjectsBiomeType { get; set; } = "";

        /// <summary>
        /// Single argument forwarded into the override <c>metaObjectsBiome.args</c>.
        /// Same conventions as <see cref="ContentBiomeArg"/>: index for
        /// <c>MatchMainObject</c>, biome name ("Sand", "Snow", …) for <c>FromList</c>.
        /// Ignored when <see cref="MetaObjectsBiomeType"/> is empty / "MatchZone".
        /// </summary>
        public string MetaObjectsBiomeArg { get; set; } = "";

        // ── T-006: per-zone caps / cutoff / content pools ───────────────────

        /// <summary>
        /// Override <c>guardCutoffValue</c> on every zone. <c>null</c> = generator
        /// default (today: 2000 on neutrals/spawns/center). Shipped templates use
        /// 2000–2500. Below the cutoff the game can drop guard stacks; raising the
        /// cutoff means more objects sit unguarded.
        /// </summary>
        public int? GuardCutoffValue { get; set; }

        /// <summary>
        /// Override <c>guardedContentPool</c> on every zone. Empty list = generator
        /// default (the tier-derived pools chosen by <c>BuildSpawnZone</c> /
        /// <c>BuildNeutralZone</c>). When non-empty, the listed content-pool SIDs
        /// replace the generator's choice on every emitted zone.
        /// <para>List of catalog SIDs like <c>template_pool_arcade_guarded_treasure_zone</c>.
        /// Round-trips through <see cref="SettingsFile"/> as a CSV string because
        /// the share codec only supports value types.</para>
        /// </summary>
        public List<string> GuardedContentPool { get; set; } = new();

        /// <summary>
        /// Override <c>unguardedContentPool</c> on every zone. Same shape and rules
        /// as <see cref="GuardedContentPool"/>.
        /// </summary>
        public List<string> UnguardedContentPool { get; set; } = new();

        /// <summary>
        /// Override <c>contentCountLimits</c> on every zone. List of catalog SIDs
        /// (e.g. <c>content_limits_spawn</c>, <c>content_limits_side</c>). Empty list
        /// = leave whatever the zone builder chose. Distinct from the global
        /// <c>ContentControlSettings.ContentCountLimits</c>, which authors fresh
        /// definitions on the template root rather than referencing existing ones.
        /// </summary>
        public List<string> ContentCountLimitRefs { get; set; } = new();

        // ── T-502: per-template overrides for the per-zone guard scalars ──

        /// <summary>
        /// Override <c>guardMultiplier</c> on every zone. <c>null</c> = generator
        /// default (the tier-/role-derived value chosen by <c>BuildSpawnZone</c> /
        /// <c>BuildNeutralZone</c> / <c>BuildHubZone</c>, then scaled by the
        /// neutral-stack-strength tuning). When set, the override stamps verbatim
        /// onto every emitted zone — no tuning scaling — matching the way the
        /// other <see cref="ZoneOverridesSettings"/> fields replace the
        /// generator's choice.
        /// </summary>
        public double? GuardMultiplier { get; set; }

        /// <summary>
        /// Override <c>guardRandomization</c> on every zone. <c>null</c> = generator
        /// default (the global <c>Settings.ZoneCfg.Advanced.GuardRandomization</c>
        /// slider value, falling back to 0.05 when Advanced mode is off). 0.0 is a
        /// meaningful "no randomization" value distinct from <c>null</c>/unset.
        /// </summary>
        public double? GuardRandomization { get; set; }

        // ── T-503: per-template overrides for the per-zone content/resource values ──
        // Both scalar and per-area variants of guardedContentValue, unguardedContentValue
        // and resourcesValue ship side-by-side from hardcoded tuning profiles. These six
        // nullable knobs let the user replace any individual number without changing the
        // others. null = generator default (tier-/role-derived, then scaled by ContentScale
        // or ResourceScale). When set, the override stamps verbatim onto every emitted
        // zone — no tuning scaling — matching the T-502 pattern.

        /// <summary>Override <c>resourcesValue</c> on every zone. <c>null</c> = generator default.</summary>
        public int? ResourcesValue { get; set; }

        /// <summary>Override <c>resourcesValuePerArea</c> on every zone. <c>null</c> = generator default.</summary>
        public int? ResourcesValuePerArea { get; set; }

        /// <summary>Override <c>guardedContentValue</c> on every zone. <c>null</c> = generator default.</summary>
        public int? GuardedContentValue { get; set; }

        /// <summary>Override <c>guardedContentValuePerArea</c> on every zone. <c>null</c> = generator default.</summary>
        public int? GuardedContentValuePerArea { get; set; }

        /// <summary>Override <c>unguardedContentValue</c> on every zone. <c>null</c> = generator default.</summary>
        public int? UnguardedContentValue { get; set; }

        /// <summary>Override <c>unguardedContentValuePerArea</c> on every zone. <c>null</c> = generator default.</summary>
        public int? UnguardedContentValuePerArea { get; set; }

        // ── T-508: per-template overrides for random-hire creature growth ──
        // Both shipped as 7-entry arrays per difficulty (Beginner…Heroic).
        // Empty list = unset; the field is omitted from emitted JSON. When
        // non-empty, the override stamps verbatim onto every zone.

        /// <summary>
        /// Override <c>randomHireEnableWeeklyUnitIncrement</c> on every zone.
        /// Empty list = generator default (field omitted). Round-trips through
        /// <see cref="SettingsFile"/> as a CSV string of <c>true</c>/<c>false</c>
        /// tokens to keep the share codec's value-equality "non-default" check
        /// sound (see <see cref="SettingsShareCodec"/>).
        /// </summary>
        public List<bool> RandomHireEnableWeeklyUnitIncrement { get; set; } = new();

        /// <summary>
        /// Override <c>randomHireInitialUnitIncrement</c> on every zone.
        /// Empty list = generator default (field omitted). CSV-of-ints in
        /// <see cref="SettingsFile"/>, same rule as
        /// <see cref="RandomHireEnableWeeklyUnitIncrement"/>.
        /// </summary>
        public List<int> RandomHireInitialUnitIncrement { get; set; } = new();
    }

    /// <summary>
    /// T-201 — encounter-holes (multi-stack battles). Today the generator hardcodes
    /// <c>GameRules.encounterHoles = false</c> and never emits per-zone
    /// <c>encounterHolesSettings</c>. Toggling <see cref="Enabled"/> flips the
    /// global flag to <c>true</c> and stamps <see cref="AffectedEncounters"/> /
    /// <see cref="TwoHoleEncounters"/> onto every zone, matching the shape used by
    /// shipped templates (Anarchy, Maze, Massacre, …): the global bool and the
    /// per-zone object always travel together. Default disabled = byte-identical
    /// to current output. The current UI applies the same per-zone numbers
    /// uniformly; a true per-zone override is deferred (would need a per-zone
    /// editor surface that does not exist today — see PR notes).
    /// </summary>
    public class EncounterHolesOptions
    {
        /// <summary>
        /// Master toggle. When false: emit <c>encounterHoles: false</c> (current
        /// hardcoded behavior) and no per-zone settings. When true: emit
        /// <c>encounterHoles: true</c> and a per-zone <c>encounterHolesSettings</c>
        /// object on every zone.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Fraction of encounters that get holes. Shipped templates use 0.66.
        /// Only emitted when <see cref="Enabled"/> is true.
        /// </summary>
        public double AffectedEncounters { get; set; } = 0.66;

        /// <summary>
        /// Fraction of affected encounters that get two holes (vs. one).
        /// Shipped templates use 0.66.
        /// Only emitted when <see cref="Enabled"/> is true.
        /// </summary>
        public double TwoHoleEncounters { get; set; } = 0.66;
    }

    public class BordersRoadsSettings
    {
        /// <summary>Variant.Border.CornerRadius override. null = generator default (0.0).</summary>
        public double? CornerRadius { get; set; }
        /// <summary>Variant.Border.ObstaclesWidth override. null = generator default (3).</summary>
        public int? ObstaclesWidth { get; set; }
        /// <summary>If true, water border is applied with WaterWidth. Default WaterType "water grass".</summary>
        public bool WaterBorderEnabled { get; set; } = false;
        /// <summary>Width of water border. Only used when WaterBorderEnabled is true.</summary>
        public int WaterWidth { get; set; } = 4;
        /// <summary>Road.Type override applied to every road. null = generator default ("Dirt").</summary>
        public string? RoadType { get; set; }
    }

    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public int PlayerCount { get; set; } = 2;
        public int MapSize { get; set; } = 160;

        /// <summary>
        /// Optional user override for <c>RmgTemplate.description</c>. Empty / null
        /// = generator default (auto-built from settings via
        /// <c>BuildTemplateDescription</c>). When non-empty, the value is emitted
        /// verbatim — no auto-formatting, no trimming. Multi-line strings are
        /// preserved exactly as authored. T-504.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Optional user override for <c>RmgTemplate.displayWinCondition</c>.
        /// Empty / null = generator default (the effective <c>VictoryCondition</c>
        /// id, e.g. <c>"win_condition_1"</c>). When non-empty, the value is
        /// emitted verbatim. Single-line. T-504.
        /// </summary>
        public string DisplayWinCondition { get; set; } = "";

        /// <summary>
        /// Optional deterministic seed. Null = non-deterministic (system random).
        /// </summary>
        public int? Seed { get; set; } = null;
        public HeroSettings HeroSettings { get; set; } = new HeroSettings();

        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public int MaxPortalConnections { get; set; } = 32;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public bool GenerateRoads { get; set; } = true;
        public bool MatchPlayerCastleFactions { get; set; } = false;
        public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        public MapTopology Topology { get; set; } = MapTopology.Random;
        public ZoneConfiguration ZoneCfg { get; set; } = new ZoneConfiguration();
        public int FactionLawsExpPercent { get; set; } = 100;
        public int AstrologyExpPercent { get; set; } = 100;
        public GameEndConditions GameEndConditions { get; set; } = new GameEndConditions();
        public GladiatorArenaRules GladiatorArenaRules { get; set; } = new GladiatorArenaRules();
        public TournamentRules TournamentRules { get; set; } = new TournamentRules();

        // ── Experimental ────────────────────────────────────────────────────────

        /// <summary>Bans hero hiring at taverns. Softer variant of SingleHero mode.</summary>
        public bool HeroHireBan { get; set; } = false;
        /// <summary>Override desertion victory-condition day. 0 = generator default (3).</summary>
        public int DesertionDay { get; set; } = 0;
        /// <summary>Override desertion victory-condition value. 0 = generator default (3000).</summary>
        public int DesertionValue { get; set; } = 0;

        public TerrainSettings Terrain { get; set; } = new TerrainSettings();
        public BuildingPresetSettings BuildingPresets { get; set; } = new BuildingPresetSettings();
        public GuardProgressionSettings GuardProgression { get; set; } = new GuardProgressionSettings();
        public GuardReactionSettings GuardReaction { get; set; } = new GuardReactionSettings();
        public NeutralCitySettings NeutralCities { get; set; } = new NeutralCitySettings();
        public ContentControlSettings Content { get; set; } = new ContentControlSettings();
        public StartingBonusSettings Bonuses { get; set; } = new StartingBonusSettings();
        public BordersRoadsSettings BordersRoads { get; set; } = new BordersRoadsSettings();
        public ConnectionDefaultsSettings ConnectionDefaults { get; set; } = new ConnectionDefaultsSettings();
        public ZoneOverridesSettings ZoneOverrides { get; set; } = new ZoneOverridesSettings();
        public EncounterHolesOptions EncounterHoles { get; set; } = new EncounterHolesOptions();
        public ZoneContentList PlayerZoneContent { get; set; } = new();
        public NeutralZoneContent NeutralZoneContent { get; set; } = new();
        public List<ZoneRoadDecoration> ZoneRoadDecorations { get; set; } = new();
    }

    public enum NeutralZoneQuality
    {
        Low,
        Medium,
        High
    }
}
