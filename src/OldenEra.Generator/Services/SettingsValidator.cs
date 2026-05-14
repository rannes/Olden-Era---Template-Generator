using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Pre-generation validation shared by the WPF and Blazor hosts.
    /// </summary>
    /// <remarks>
    /// Mirrors the rules in MainWindow.Validate(). Blockers prevent generation;
    /// warnings advise the user but do not block. Each issue carries a
    /// <see cref="ValidationIssue.FieldKey"/> so UIs can highlight the offending
    /// control and (where relevant) wire a one-click fix.
    /// </remarks>
    public static class SettingsValidator
    {
        public const int DefaultMaxZones = 32;

        public enum Severity { Blocker, Warning }

        /// <summary>
        /// A single validation finding. <see cref="FieldKey"/> is one of the
        /// stable identifiers in <see cref="ValidationFieldKeys"/>; it is the
        /// contract between the validator and any UI that wants to highlight a
        /// control or attach a fix action. <see cref="Code"/> is an optional,
        /// stable discriminator (see <see cref="ValidationIssueCodes"/>) for
        /// rules that share a <see cref="FieldKey"/> but require different UI
        /// affordances — UIs must not match on <see cref="Message"/> substrings.
        /// </summary>
        public sealed record ValidationIssue(string FieldKey, Severity Severity, string Message, string? Code = null);

        /// <summary>
        /// <see cref="Blockers"/> and <see cref="Warnings"/> remain
        /// flat string lists for back-compat with existing callers that just
        /// want to render messages. <see cref="Issues"/> carries the same
        /// information plus a field-binding key for inline highlighting.
        /// </summary>
        public sealed record Result(
            IReadOnlyList<string> Blockers,
            IReadOnlyList<string> Warnings,
            IReadOnlyList<ValidationIssue> Issues)
        {
            public Result(IReadOnlyList<string> blockers, IReadOnlyList<string> warnings)
                : this(blockers, warnings, Array.Empty<ValidationIssue>()) { }

            public bool IsValid => Blockers.Count == 0;

            /// <summary>Returns issues bound to the given field key (any severity).</summary>
            public IEnumerable<ValidationIssue> ForField(string fieldKey) =>
                Issues.Where(i => string.Equals(i.FieldKey, fieldKey, StringComparison.Ordinal));

            /// <summary>True if any blocker is bound to <paramref name="fieldKey"/>.</summary>
            public bool HasBlockerOn(string fieldKey) =>
                Issues.Any(i => i.Severity == Severity.Blocker && string.Equals(i.FieldKey, fieldKey, StringComparison.Ordinal));

            /// <summary>True if any warning is bound to <paramref name="fieldKey"/>.</summary>
            public bool HasWarningOn(string fieldKey) =>
                Issues.Any(i => i.Severity == Severity.Warning && string.Equals(i.FieldKey, fieldKey, StringComparison.Ordinal));
        }

        public static Result Validate(GeneratorSettings settings, int? maxZonesOverride = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var issues = new List<ValidationIssue>();

            int players = settings.PlayerCount;
            int neutral = TotalNeutralZones(settings);
            int maxZones = maxZonesOverride ?? DefaultMaxZones;

            void Block(string field, string msg, string? code = null) => issues.Add(new ValidationIssue(field, Severity.Blocker, msg, code));
            void Warn(string field, string msg, string? code = null) => issues.Add(new ValidationIssue(field, Severity.Warning, msg, code));

            if (settings.HeroSettings.HeroCountMin > settings.HeroSettings.HeroCountMax)
            {
                Block(ValidationFieldKeys.HeroMinMax, "Min Heroes cannot be greater than Max Heroes.");
            }

            if (players + neutral > maxZones)
            {
                Block(ValidationFieldKeys.ZonesTotal, $"Total zones (players + neutral) cannot exceed {maxZones}.");
            }

            if (string.IsNullOrWhiteSpace(settings.TemplateName))
            {
                Block(ValidationFieldKeys.TemplateName, "Template name cannot be empty.");
            }

            bool cityHoldActive = settings.GameEndConditions.CityHold
                || settings.GameEndConditions.VictoryCondition == "win_condition_5";
            if (cityHoldActive && settings.Topology != MapTopology.HubAndSpoke && neutral == 0)
            {
                Block(ValidationFieldKeys.NeutralZoneCount,
                    "City Hold requires at least one neutral zone to place the hold city. Add a neutral zone or switch to the Hub layout.",
                    ValidationIssueCodes.NeutralZoneCountForCityHold);
            }

            if (settings.NoDirectPlayerConnections && neutral == 0)
            {
                Block(ValidationFieldKeys.NeutralZoneCount,
                    "\"Connect via neutral zones only\" requires at least one neutral zone. Add a neutral zone or disable this option.");
            }

            if (settings.GameEndConditions.VictoryCondition == "win_condition_6" && players != 2)
            {
                Block(ValidationFieldKeys.PlayerCount,
                    "Tournament mode only supports exactly 2 players.");
            }

            if (settings.TemplateName.Trim().Equals("Custom Template", StringComparison.OrdinalIgnoreCase))
            {
                Warn(ValidationFieldKeys.TemplateName,
                    "⚠️ The template is still using the default name \"Custom Template\". Consider renaming it before saving.");
            }

            int totalZones = players + neutral;
            int totalZonesIncludingHub = settings.Topology == MapTopology.HubAndSpoke ? totalZones + 1 : totalZones;
            int mapSize = settings.MapSize;
            if (totalZonesIncludingHub > 0 && (mapSize * mapSize) / totalZonesIncludingHub < 1024)
            {
                Warn(ValidationFieldKeys.MapSize,
                    "⚠️ Estimated zone size is too small. The game may freeze when loading the map. Increase the map size or reduce the number of zones.");
            }

            if (mapSize > KnownValues.MaxOfficialMapSize)
            {
                Warn(ValidationFieldKeys.MapSize,
                    "Experimental map sizes above 240x240 are not confirmed by official templates; generated maps may fail, freeze, or behave unpredictably in game.");
            }

            if (totalZones > 10)
            {
                int playerCastles = settings.ZoneCfg.PlayerZoneCastles;
                int neutralCastles = settings.ZoneCfg.Advanced.Enabled ? 0 : settings.ZoneCfg.NeutralZoneCastles;
                if (playerCastles > 1 || neutralCastles > 1)
                {
                    Warn(ValidationFieldKeys.PlayerZoneCastles,
                        "⚠️ Using more than 1 castle per zone with more than 10 total zones may cause the game to freeze when generating the map. Consider reducing the number of castles.");
                }
            }

            if (settings.ZoneCfg.Advanced.Enabled
                && settings.MinNeutralZonesBetweenPlayers > 0
                && !TemplateGenerator.CanHonorNeutralSeparation(settings, neutral))
            {
                Warn(ValidationFieldKeys.MinNeutralSeparation,
                    "Minimum neutral separation cannot be guaranteed with the current layout, neutral zone total, or portal setting; generation will ignore that option.");
            }

            if (settings.Topology == MapTopology.Balanced && totalZones >= 24)
            {
                Warn(ValidationFieldKeys.Topology,
                    "Balanced layout may produce unexpected results with more than 24 total zones.");
            }

            // Hero ban / fixed-hero validation.
            var heroBans = settings.HeroSettings.HeroBans;
            if (heroBans.Count > 0)
            {
                var bansSet = new HashSet<string>(heroBans, StringComparer.OrdinalIgnoreCase);
                var catalog = CommunityCatalog.Default;
                foreach (var faction in catalog.Factions)
                {
                    var heroes = catalog.HeroesByFaction(faction.Id).ToList();
                    if (heroes.Count == 0) continue;
                    if (heroes.All(h => bansSet.Contains(h.Id)))
                    {
                        Warn(ValidationFieldKeys.HeroBans,
                            $"⚠️ Every hero of faction \"{faction.Name}\" is banned. The game may fail to assign a starting hero to that faction.");
                    }
                }

                // Pinned-hero-and-banned cross-check: blocker.
                foreach (var kv in settings.HeroSettings.FixedStartingHeroByFaction)
                {
                    var fixedId = kv.Value;
                    if (string.IsNullOrWhiteSpace(fixedId)) continue;
                    if (bansSet.Contains(fixedId))
                    {
                        var factionName = catalog.Factions
                            .FirstOrDefault(f => string.Equals(f.Id, kv.Key, StringComparison.OrdinalIgnoreCase))
                            ?.Name ?? kv.Key;
                        Block(ValidationFieldKeys.HeroFixedStarting,
                            $"Pinned starting hero \"{fixedId}\" for faction \"{factionName}\" is also in the hero ban list. Remove it from one of the two.");
                    }
                }
            }

            // T-206: per-player starting bonus overrides. Warn (don't block) on
            // out-of-range slots, duplicate slots (last-write-wins), and rows
            // that set no fields. The emitter mirrors these rules.
            if (settings.Bonuses.PerPlayerOverrides.Count > 0)
            {
                var seenSlots = new HashSet<int>();
                foreach (var row in settings.Bonuses.PerPlayerOverrides)
                {
                    if (row.PlayerSlot < 1 || row.PlayerSlot > players)
                    {
                        Warn(ValidationFieldKeys.BonusPerPlayerOverrides,
                            $"Per-player bonus override targets slot {row.PlayerSlot} but template has only {players} players. The row will be skipped.");
                    }
                    else if (!seenSlots.Add(row.PlayerSlot))
                    {
                        Warn(ValidationFieldKeys.BonusPerPlayerOverrides,
                            $"Duplicate per-player bonus override for slot {row.PlayerSlot}; the later row wins.");
                    }
                    if (BonusRowIsEmpty(row.Bonuses))
                    {
                        Warn(ValidationFieldKeys.BonusPerPlayerOverrides,
                            $"Per-player bonus override for slot {row.PlayerSlot} has no fields set. Remove it or fill in a value.");
                    }
                }
            }

            var blockers = issues.Where(i => i.Severity == Severity.Blocker).Select(i => i.Message).ToList();
            var warnings = issues.Where(i => i.Severity == Severity.Warning).Select(i => i.Message).ToList();
            return new Result(blockers, warnings, issues);
        }

        private static bool BonusRowIsEmpty(StartingBonusSettings b) =>
            b.Resources.Count == 0
            && b.HeroAttack == 0 && b.HeroDefense == 0
            && b.HeroSpellpower == 0 && b.HeroKnowledge == 0
            && string.IsNullOrWhiteSpace(b.ItemSid)
            && string.IsNullOrWhiteSpace(b.SpellSid)
            && b.UnitMultiplier == 0.0;

        public static int TotalNeutralZones(GeneratorSettings settings)
        {
            if (!settings.ZoneCfg.Advanced.Enabled)
                return settings.ZoneCfg.NeutralZoneCount;

            var a = settings.ZoneCfg.Advanced;
            return a.NeutralLowNoCastleCount + a.NeutralLowCastleCount
                 + a.NeutralMediumNoCastleCount + a.NeutralMediumCastleCount
                 + a.NeutralHighNoCastleCount + a.NeutralHighCastleCount;
        }
    }

    /// <summary>
    /// Stable string identifiers for fields the validator can flag. UI hosts map
    /// these to a control (CSS id, WPF x:Name, etc.) so messages can render
    /// inline on the offending input. Add a new constant here when adding a new
    /// validation rule that should anchor on a specific control.
    /// </summary>
    public static class ValidationFieldKeys
    {
        public const string TemplateName = "template.name";
        public const string MapSize = "map.size";
        public const string PlayerCount = "map.players";
        public const string Topology = "topology";

        public const string NeutralZoneCount = "zones.neutral.count";
        public const string ZonesTotal = "zones.total";
        public const string PlayerZoneCastles = "zones.player.castles";
        public const string MinNeutralSeparation = "zones.minSeparation";

        public const string HeroMinMax = "hero.minMax";
        public const string HeroBans = "hero.bans";
        public const string HeroFixedStarting = "hero.fixedStarting";

        public const string BonusPerPlayerOverrides = "bonuses.perPlayerOverrides";
    }

    /// <summary>
    /// Stable discriminator codes for rules that share a <see cref="ValidationFieldKeys"/>
    /// but need different UI affordances (e.g., the City-Hold variant of the
    /// neutral-zone-count blocker also offers "switch to Hub layout"). Add a
    /// new code only when a UI needs to distinguish two rules anchored on the
    /// same field — do not blanket-add codes for every rule.
    /// </summary>
    public static class ValidationIssueCodes
    {
        /// <summary>City-Hold variant of the neutral-zone-count blocker.</summary>
        public const string NeutralZoneCountForCityHold = "neutral.count.cityHold";
    }

    /// <summary>
    /// Mechanical fixes the UI can offer for specific blockers. Keep this set
    /// small: only add entries where the remediation is single-click obvious
    /// and unambiguous. Each fix mutates settings in place; the host is
    /// responsible for re-validating and re-rendering afterwards.
    /// </summary>
    public static class ValidationFixes
    {
        /// <summary>Increment the neutral zone count by one (simple-mode bucket).</summary>
        public static void AddNeutralZone(GeneratorSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (settings.ZoneCfg.Advanced.Enabled)
            {
                // In advanced mode the simple count is unused. Bump the medium-no-castle
                // tier as the most neutral default — users can re-balance after.
                settings.ZoneCfg.Advanced.NeutralMediumNoCastleCount += 1;
            }
            else
            {
                settings.ZoneCfg.NeutralZoneCount += 1;
            }
        }

        /// <summary>Switch the topology to Hub-and-Spoke (resolves "City Hold needs neutrals").</summary>
        public static void SwitchToHubTopology(GeneratorSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.Topology = MapTopology.HubAndSpoke;
        }

        /// <summary>Set the template name to a non-empty placeholder so the blocker clears.</summary>
        public static void SetDefaultTemplateName(GeneratorSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings.TemplateName = "Custom Template";
        }
    }
}
