using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Reference data sourced from the alcaras/homm-olden community datamine.
    /// Held in memory after first access; no network or disk I/O at runtime.
    /// </summary>
    /// <remarks>
    /// The JSON files live alongside this assembly as embedded resources
    /// (see <c>OldenEra.Generator.csproj</c>). Re-fetch via the script at
    /// <c>src/OldenEra.Generator/CommunityData/scripts/fetch-from-alcaras.py</c>.
    /// </remarks>
    public sealed class CommunityCatalog
    {
        public IReadOnlyList<HeroEntry> Heroes { get; }
        public IReadOnlyList<UnitEntry> Units { get; }
        public IReadOnlyList<SpellEntry> Spells { get; }
        public IReadOnlyList<SkillEntry> Skills { get; }
        public IReadOnlyList<SubclassEntry> Subclasses { get; }
        public IReadOnlyList<FactionEntry> Factions { get; }
        public IReadOnlyList<SkillColumnEntry> SkillColumns { get; }
        public IReadOnlyList<ClassEntry> Classes { get; }
        public IReadOnlyList<SpecializationEntry> Specializations { get; }

        private CommunityCatalog(
            IReadOnlyList<HeroEntry> heroes,
            IReadOnlyList<UnitEntry> units,
            IReadOnlyList<SpellEntry> spells,
            IReadOnlyList<SkillEntry> skills,
            IReadOnlyList<SubclassEntry> subclasses,
            IReadOnlyList<FactionEntry> factions,
            IReadOnlyList<SkillColumnEntry> skillColumns,
            IReadOnlyList<ClassEntry> classes,
            IReadOnlyList<SpecializationEntry> specializations)
        {
            Heroes = heroes;
            Units = units;
            Spells = spells;
            Skills = skills;
            Subclasses = subclasses;
            Factions = factions;
            SkillColumns = skillColumns;
            Classes = classes;
            Specializations = specializations;

            _spellSchools = new Lazy<IReadOnlyList<string>>(() =>
                Spells.Select(s => s.School ?? "")
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(SpellSchoolOrder)
                      .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                      .ToList());
        }

        private static readonly Lazy<CommunityCatalog> _instance = new(LoadFromEmbedded);

        public static CommunityCatalog Default => _instance.Value;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static CommunityCatalog LoadFromEmbedded()
        {
            return new CommunityCatalog(
                heroes: LoadArray<HeroEntry>("heroes.json"),
                units: LoadArray<UnitEntry>("units.json"),
                spells: LoadArray<SpellEntry>("spells.json"),
                skills: LoadArray<SkillEntry>("skills.json"),
                subclasses: LoadArray<SubclassEntry>("subclasses.json"),
                factions: LoadArray<FactionEntry>("factions.json"),
                skillColumns: LoadArray<SkillColumnEntry>("skill-columns.json"),
                classes: LoadArray<ClassEntry>("classes.json"),
                specializations: LoadArray<SpecializationEntry>("specializations.json"));
        }

        private static IReadOnlyList<T> LoadArray<T>(string fileName)
        {
            var asm = typeof(CommunityCatalog).Assembly;
            string resourceName = $"OldenEra.Generator.CommunityData.{fileName}";
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. "
                    + "Verify the .csproj <EmbeddedResource Include=\"CommunityData\\*.json\" /> entry.");
            return JsonSerializer.Deserialize<List<T>>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize {fileName}.");
        }

        // ── Filtering helpers ────────────────────────────────────────────────

        public IEnumerable<HeroEntry> HeroesByFaction(string factionId) =>
            Heroes.Where(h => string.Equals(h.Faction, factionId, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<SpellEntry> SpellsBySchool(string school) =>
            Spells.Where(s => string.Equals(s.School, school, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<UnitEntry> UnitsByFaction(string factionId) =>
            Units.Where(u => string.Equals(u.Faction, factionId, StringComparison.OrdinalIgnoreCase));

        // ── Spell school taxonomy ────────────────────────────────────────────

        /// <summary>
        /// Distinct spell schools in canonical display order
        /// (Day, Night, Arcane, Primal, then any others alphabetically).
        /// </summary>
        public IReadOnlyList<string> SpellSchools => _spellSchools.Value;
        private readonly Lazy<IReadOnlyList<string>> _spellSchools;

        public static int SpellSchoolOrder(string? school) => school?.ToLowerInvariant() switch
        {
            "day" => 0,
            "night" => 1,
            "arcane" => 2,
            "primal" => 3,
            _ => 99,
        };

        public static string FriendlySpellSchool(string? school) => school?.ToLowerInvariant() switch
        {
            "day" => "Day",
            "night" => "Night",
            "arcane" => "Arcane",
            "primal" => "Primal",
            _ => string.IsNullOrEmpty(school) ? "Other" : char.ToUpper(school[0]) + school[1..],
        };
    }

    public sealed record FactionEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("unitKey")] string UnitKey,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("skill")] string Skill,
        [property: JsonPropertyName("might")] string MightClass,
        [property: JsonPropertyName("magic")] string MagicClass);

    /// <summary>
    /// Catalog entry for a hero. The first six members
    /// (<see cref="Id"/>, <see cref="Faction"/>, <see cref="Kind"/>,
    /// <see cref="Name"/>, <see cref="Specialty"/>,
    /// <see cref="SpecialtyDescription"/>) are positional and MUST NOT be
    /// reordered — existing pickers and tests depend on positional
    /// construction. Stat/army/skill payload (T-603) is appended.
    /// </summary>
    public sealed record HeroEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("faction")] string Faction,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("specialty")] string? Specialty,
        [property: JsonPropertyName("specDesc")] string? SpecialtyDescription,
        // ── T-603: starter loadout from heroes.json ─────────────────────────
        [property: JsonPropertyName("specId")] string? SpecId = null,
        [property: JsonPropertyName("armyScore")] int ArmyScore = 0,
        [property: JsonPropertyName("stats")] HeroStats? Stats = null,
        [property: JsonPropertyName("skills")] IReadOnlyList<string>? Skills = null,
        [property: JsonPropertyName("army")] string? Army = null)
    {
        /// <summary>
        /// Compact picker tooltip. Lists specialty (existing behaviour),
        /// the A/D/P/K stat line, the starter army-score, and the starter
        /// skill loadout. T-803 will replace this with a richer tooltip
        /// system later.
        /// </summary>
        public string TooltipText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Name);
            if (!string.IsNullOrWhiteSpace(Specialty))
                sb.Append(" — ").Append(Specialty);
            if (Stats is not null)
            {
                sb.AppendLine();
                sb.Append("A ").Append(Stats.A)
                  .Append(" / D ").Append(Stats.D)
                  .Append(" / P ").Append(Stats.P)
                  .Append(" / K ").Append(Stats.K);
            }
            if (ArmyScore > 0)
            {
                sb.AppendLine();
                sb.Append("Army score ").Append(ArmyScore);
            }
            if (Skills is { Count: > 0 })
            {
                sb.AppendLine();
                sb.Append("Skills: ").Append(string.Join(", ", Skills));
            }
            if (!string.IsNullOrWhiteSpace(SpecialtyDescription))
            {
                sb.AppendLine();
                sb.Append(SpecialtyDescription);
            }
            return sb.ToString();
        }
    }

    /// <summary>Hero starter stats (Attack / Defense / Power / Knowledge).</summary>
    public sealed record HeroStats(
        [property: JsonPropertyName("A")] int A,
        [property: JsonPropertyName("D")] int D,
        [property: JsonPropertyName("P")] int P,
        [property: JsonPropertyName("K")] int K);

    /// <summary>
    /// Catalog entry for a creature. The first five members
    /// (<see cref="Id"/>, <see cref="Name"/>, <see cref="Faction"/>,
    /// <see cref="Tier"/>, <see cref="Variant"/>) are positional and MUST NOT
    /// be reordered — existing pickers and tests depend on positional
    /// construction. Combat stats (T-602) and narrative payload are appended.
    /// </summary>
    public sealed record UnitEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("faction")] string Faction,
        [property: JsonPropertyName("tier")] int Tier,
        [property: JsonPropertyName("variant")] string? Variant,
        // ── T-602: combat stats from units.json ─────────────────────────────
        [property: JsonPropertyName("attack")] string? Attack = null,
        [property: JsonPropertyName("hp")] int Hp = 0,
        [property: JsonPropertyName("off")] int Off = 0,
        [property: JsonPropertyName("def")] int Def = 0,
        [property: JsonPropertyName("dmgMin")] int DmgMin = 0,
        [property: JsonPropertyName("dmgMax")] int DmgMax = 0,
        [property: JsonPropertyName("init")] int Init = 0,
        [property: JsonPropertyName("speed")] int Speed = 0,
        [property: JsonPropertyName("squadValue")] int SquadValue = 0,
        [property: JsonPropertyName("cost")] int Cost = 0,
        [property: JsonPropertyName("ai")] string? Ai = null,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags = null,
        [property: JsonPropertyName("narrative")] string? Narrative = null,
        [property: JsonPropertyName("passives")] IReadOnlyList<UnitAbilityEntry>? Passives = null,
        [property: JsonPropertyName("abilities")] IReadOnlyList<UnitAbilityEntry>? Abilities = null)
    {
        /// <summary>
        /// Human-readable tooltip text for picker chips (Web title attr,
        /// WPF ToolTip). Compact multi-line summary; lists at most one
        /// active ability so the tooltip stays scannable. T-803 will
        /// replace this with a richer tooltip system later.
        /// </summary>
        public string TooltipText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('T').Append(Tier).Append(' ').Append(Name);
            if (!string.IsNullOrEmpty(Variant)) sb.Append(" (").Append(Variant).Append(')');
            if (Hp > 0)
            {
                sb.AppendLine();
                if (!string.IsNullOrEmpty(Attack)) sb.Append(Attack).Append(" • ");
                sb.Append("HP ").Append(Hp)
                  .Append(" • Off ").Append(Off).Append(" / Def ").Append(Def);
                sb.AppendLine();
                sb.Append("Dmg ").Append(DmgMin).Append('-').Append(DmgMax)
                  .Append(" • Init ").Append(Init).Append(" • Speed ").Append(Speed);
                sb.AppendLine();
                sb.Append("Squad ").Append(SquadValue).Append(" • Cost ").Append(Cost);
            }
            if (Abilities is { Count: > 0 })
            {
                sb.AppendLine();
                sb.Append("Ability: ").Append(Abilities[0].Name);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Passive trait or active ability on a creature — both use the same
    /// {name, desc} shape in <c>units.json</c>.
    /// </summary>
    public sealed record UnitAbilityEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("desc")] string? Description);

    /// <summary>
    /// Catalog entry for a spell. The first six members
    /// (<see cref="Id"/>, <see cref="Name"/>, <see cref="School"/>,
    /// <see cref="Tier"/>, <see cref="Scope"/>, <see cref="Description"/>)
    /// are positional and MUST NOT be reordered. Cost/icon/magic-type
    /// payload (T-603) is appended.
    /// </summary>
    public sealed record SpellEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("school")] string School,
        [property: JsonPropertyName("tier")] int Tier,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("desc")] string? Description,
        // ── T-603: cost / icon / magic-type from spells.json ────────────────
        [property: JsonPropertyName("manaCost")] IReadOnlyList<int>? ManaCost = null,
        [property: JsonPropertyName("cooldown")] int Cooldown = 0,
        [property: JsonPropertyName("learnCost")] IReadOnlyList<SpellResourceCost>? LearnCost = null,
        [property: JsonPropertyName("icon")] string? Icon = null,
        [property: JsonPropertyName("magicType")] string? MagicType = null)
    {
        /// <summary>
        /// Compact picker tooltip. Tier + name first, then mana cost
        /// (showing the per-skill-level array when it varies), cooldown,
        /// magic type, and finally the description. T-803 will replace
        /// this later with a richer tooltip system.
        /// </summary>
        public string TooltipText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('T').Append(Tier).Append(' ').Append(Name);
            if (!string.IsNullOrWhiteSpace(MagicType))
                sb.Append(" · ").Append(MagicType);
            if (ManaCost is { Count: > 0 })
            {
                sb.AppendLine();
                sb.Append("Mana ");
                bool uniform = ManaCost.Distinct().Count() == 1;
                sb.Append(uniform ? ManaCost[0].ToString() : string.Join("/", ManaCost));
                if (Cooldown > 0)
                    sb.Append(" · CD ").Append(Cooldown);
            }
            else if (Cooldown > 0)
            {
                sb.AppendLine();
                sb.Append("CD ").Append(Cooldown);
            }
            if (!string.IsNullOrWhiteSpace(Description))
            {
                sb.AppendLine();
                sb.Append(Description);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Resource cost line in <c>spells.json</c> learnCost arrays
    /// (e.g. {"name":"crystals","cost":2}).
    /// </summary>
    public sealed record SpellResourceCost(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("cost")] int Cost);

    /// <summary>
    /// Catalog entry for a hero skill. The first five members
    /// (<see cref="Id"/>, <see cref="Name"/>, <see cref="Group"/>,
    /// <see cref="SkillType"/>, <see cref="FactionId"/>) are positional
    /// and MUST NOT be reordered. Description/level/subclass/starter
    /// payload (T-603) is appended.
    /// </summary>
    public sealed record SkillEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("group")] string Group,
        [property: JsonPropertyName("skillType")] string SkillType,
        [property: JsonPropertyName("factionId")] string? FactionId,
        // ── T-603: descriptions / progression / cross-refs ──────────────────
        [property: JsonPropertyName("baseDesc")] string? BaseDesc = null,
        [property: JsonPropertyName("levels")] IReadOnlyList<SkillLevelEntry>? Levels = null,
        [property: JsonPropertyName("subclasses")] IReadOnlyList<SkillSubclassRef>? Subclasses = null,
        [property: JsonPropertyName("starters")] IReadOnlyList<SkillStarterRef>? Starters = null)
    {
        /// <summary>
        /// Compact picker tooltip. Name + base description first, then
        /// the count of trained subclasses / starter heroes so picker
        /// callers can hint at the cross-references without dumping the
        /// full list.
        /// </summary>
        public string TooltipText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Name);
            if (!string.IsNullOrWhiteSpace(BaseDesc))
            {
                sb.AppendLine();
                sb.Append(BaseDesc);
            }
            int levels = Levels?.Count ?? 0;
            int subs = Subclasses?.Count ?? 0;
            int starters = Starters?.Count ?? 0;
            if (levels + subs + starters > 0)
            {
                sb.AppendLine();
                sb.Append(levels).Append(" tier(s) · ")
                  .Append(subs).Append(" subclass(es) · ")
                  .Append(starters).Append(" starter(s)");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// One tier of a skill (Basic / Advanced / Expert / Mastery).
    /// Sub-skills picked at this tier live under <see cref="Subskills"/>.
    /// </summary>
    public sealed record SkillLevelEntry(
        [property: JsonPropertyName("level")] int Level,
        [property: JsonPropertyName("icon")] string? Icon,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("desc")] string? Description,
        [property: JsonPropertyName("bonuses")] IReadOnlyList<string>? Bonuses,
        [property: JsonPropertyName("subskills")] IReadOnlyList<SkillSubskillEntry>? Subskills);

    public sealed record SkillSubskillEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("desc")] string? Description);

    /// <summary>
    /// Subclass cross-reference: which subclass trains in this skill.
    /// Mirrors the <c>{name, class, faction, kind}</c> shape in skills.json.
    /// </summary>
    public sealed record SkillSubclassRef(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("class")] string? Class,
        [property: JsonPropertyName("faction")] string? Faction,
        [property: JsonPropertyName("kind")] string? Kind);

    /// <summary>
    /// Starter cross-reference: heroes that begin with this skill.
    /// Mirrors the <c>{id, name, faction, kind, level}</c> shape in skills.json.
    /// </summary>
    public sealed record SkillStarterRef(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("faction")] string? Faction,
        [property: JsonPropertyName("kind")] string? Kind,
        [property: JsonPropertyName("level")] int Level);

    /// <summary>
    /// Skill-tree column metadata (e.g. OFF/Offense/combat). Sourced from
    /// <c>skill-columns.json</c>; used by upcoming tooltip groupings (T-602/T-603).
    /// </summary>
    public sealed record SkillColumnEntry(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("group")] string Group);

    public sealed record SubclassEntry(
        [property: JsonPropertyName("faction")] string Faction,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("class")] string Class,
        [property: JsonPropertyName("skills")] IReadOnlyList<string> Skills,
        [property: JsonPropertyName("effect")] string Effect);

    /// <summary>
    /// Hero class metadata sourced from <c>catalog/out/classes.json</c>
    /// (12 entries: one might + one magic class per faction). Carries
    /// the primary stat priors and the subclass map; the full skill-roll
    /// weight table is preserved in the on-disk JSON for future use but
    /// not surfaced here yet — keeping the C# surface focused on fields
    /// that are stable across upstream refreshes.
    /// </summary>
    public sealed record ClassEntry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("factionId")] string FactionId,
        [property: JsonPropertyName("classType")] string ClassType,
        [property: JsonPropertyName("attack")] int Attack,
        [property: JsonPropertyName("defence")] int Defence,
        [property: JsonPropertyName("power")] int Power,
        [property: JsonPropertyName("knowledge")] int Knowledge,
        [property: JsonPropertyName("statsBreakpointLevel")] int StatsBreakpointLevel,
        [property: JsonPropertyName("subclasses")] IReadOnlyDictionary<string, ClassSubclassEntry>? Subclasses);

    public sealed record ClassSubclassEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("skills")] IReadOnlyList<string> Skills,
        [property: JsonPropertyName("effect")] string Effect);

    /// <summary>
    /// Hero specialization metadata sourced from
    /// <c>catalog/out/specializations.json</c>. 126 entries keyed by
    /// <see cref="Id"/>; pairs with the upcoming T-603 <c>HeroEntry.specId</c>
    /// to surface descriptions in the picker.
    /// </summary>
    public sealed record SpecializationEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("faction")] string? Faction,
        [property: JsonPropertyName("classType")] string? ClassType,
        [property: JsonPropertyName("heroes")] IReadOnlyList<SpecializationHeroRef>? Heroes);

    public sealed record SpecializationHeroRef(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("faction")] string? Faction,
        [property: JsonPropertyName("classType")] string? ClassType);
}
