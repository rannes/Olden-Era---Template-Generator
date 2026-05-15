using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class CommunityCatalogTests
{
    private static CommunityCatalog Catalog => CommunityCatalog.Default;

    [Fact]
    public void LoadsAllCollectionsNonEmpty()
    {
        Assert.NotEmpty(Catalog.Heroes);
        Assert.NotEmpty(Catalog.Units);
        Assert.NotEmpty(Catalog.Spells);
        Assert.NotEmpty(Catalog.Skills);
        Assert.NotEmpty(Catalog.Subclasses);
        Assert.NotEmpty(Catalog.Factions);
    }

    [Fact]
    public void HeroCount_Matches108()
    {
        Assert.Equal(108, Catalog.Heroes.Count);
    }

    [Fact]
    public void FactionCount_Matches6()
    {
        Assert.Equal(6, Catalog.Factions.Count);
    }

    [Fact]
    public void Heroes_HaveExpectedSidFormat()
    {
        // SID is "<unitKey>_hero_<n>" per the alcaras dump.
        // Example: "human_hero_1", "necro_hero_12".
        var sample = Catalog.Heroes.First(h => h.Faction == "temple");
        Assert.Matches(@"^[a-z_]+_hero_\d+$", sample.Id);
    }

    [Fact]
    public void HeroesByFaction_PartitionsAllHeroes()
    {
        // Every hero belongs to a known faction; sum across factions == total.
        var factionIds = Catalog.Factions.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(Catalog.Heroes, h => Assert.Contains(h.Faction, factionIds));

        int sum = factionIds.Sum(fid => Catalog.HeroesByFaction(fid).Count());
        Assert.Equal(Catalog.Heroes.Count, sum);
    }

    [Fact]
    public void Spells_HaveSchoolAndTier()
    {
        Assert.All(Catalog.Spells, s =>
        {
            Assert.False(string.IsNullOrEmpty(s.School), $"Spell {s.Id} has no school.");
            Assert.True(s.Tier >= 1, $"Spell {s.Id} has invalid tier {s.Tier}.");
        });
    }

    [Fact]
    public void SpellsBySchool_FindsExpectedSchools()
    {
        // Game has day, night, arcane, primal magic schools (per skill-columns.json).
        var schools = Catalog.Spells.Select(s => s.School).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("day", schools);
        Assert.Contains("night", schools);
    }

    [Fact]
    public void Default_IsCachedSingleton()
    {
        Assert.Same(CommunityCatalog.Default, CommunityCatalog.Default);
    }

    // ── T-204 regression net ────────────────────────────────────────────────
    // units.json carries tier-8 neutral creatures (avatars + lich_dragon).
    // Both unit-ban pickers (Web UnitBanGrid, WPF ExperimentalPanel) group
    // by faction × tier dynamically off CommunityCatalog.Units, so any new
    // tier introduced upstream surfaces automatically. This test pins that
    // invariant: if tier-8 neutrals disappear from the catalog (e.g. a
    // catalog refresh drops them), the pickers regress silently — fail loudly.
    [Fact]
    public void Units_Tier8_AreNeutralAndReachableViaCatalog()
    {
        var tier8 = Catalog.Units.Where(u => u.Tier == 8).ToList();

        Assert.NotEmpty(tier8);
        Assert.All(tier8, u =>
            Assert.Equal("neutral", u.Faction, ignoreCase: true));

        // Same projection the unit-ban grid uses (UnitsByFaction → GroupBy Tier).
        var neutralTiers = Catalog.UnitsByFaction("neutral")
            .Select(u => u.Tier)
            .ToHashSet();
        Assert.Contains(8, neutralTiers);
    }

    // ── T-601 ───────────────────────────────────────────────────────────────
    // skill-columns.json ships with the assembly but was never loaded.
    // Pin the count + a sentinel entry so future catalog refreshes can't
    // silently drop columns the upcoming tooltip groupings depend on.
    [Fact]
    public void SkillColumns_LoadedWithExpectedCount()
    {
        Assert.Equal(20, Catalog.SkillColumns.Count);
    }

    [Fact]
    public void SkillColumns_ContainsOffenseInCombatGroup()
    {
        var off = Catalog.SkillColumns.Single(c =>
            string.Equals(c.Key, "OFF", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Offense", off.Name);
        Assert.Equal("combat", off.Group);
    }

    // ── T-604 ───────────────────────────────────────────────────────────────
    // catalog/out/{classes,specializations}.json mirror the alcaras catalog
    // generator output: 12 hero classes (one might + one magic per faction)
    // and 126 specializations keyed by spec id. Pin counts so any upstream
    // shape change surfaces in CI rather than silently degrading the
    // upcoming T-603 hero picker.
    [Fact]
    public void Classes_LoadedWithExpectedCount()
    {
        Assert.Equal(12, Catalog.Classes.Count);
    }

    [Fact]
    public void Classes_ContainsKnightHumanMight()
    {
        var knight = Catalog.Classes.Single(c => c.Name == "Knight");
        Assert.Equal("human", knight.FactionId);
        Assert.Equal("might", knight.ClassType);
        Assert.NotNull(knight.Subclasses);
        Assert.Contains("Swashbuckler", knight.Subclasses!.Keys);
    }

    [Fact]
    public void Classes_PartitionByFactionAndType()
    {
        // Each faction publishes exactly one might + one magic class.
        var byFaction = Catalog.Classes
            .GroupBy(c => c.FactionId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.ClassType).ToList());

        Assert.Equal(6, byFaction.Count);
        foreach (var (faction, types) in byFaction)
        {
            Assert.Contains("might", types);
            Assert.Contains("magic", types);
            Assert.Equal(2, types.Count);
        }
    }

    [Fact]
    public void Specializations_LoadedWithExpectedCount()
    {
        Assert.Equal(126, Catalog.Specializations.Count);
    }

    [Fact]
    public void Specializations_HaveUniqueIds()
    {
        var ids = Catalog.Specializations.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // ── T-602 ───────────────────────────────────────────────────────────────
    // UnitEntry now exposes combat stats + passives/abilities so the unit-ban
    // pickers can render tooltips. Pin a known entry's stats so a catalog
    // refresh that drops or renames any of these fields fails loudly.
    [Fact]
    public void Units_LoadCombatStats_FromKnownEntry()
    {
        var u = Catalog.Units.Single(x => x.Id == "esquire_upg");
        Assert.Equal("Guard Captain", u.Name);
        Assert.Equal("temple", u.Faction);
        Assert.Equal(1, u.Tier);
        Assert.Equal("upg", u.Variant);
        Assert.Equal("Melee", u.Attack);
        Assert.Equal(12, u.Hp);
        Assert.Equal(6, u.Off);
        Assert.Equal(5, u.Def);
        Assert.Equal(2, u.DmgMin);
        Assert.Equal(3, u.DmgMax);
        Assert.Equal(6, u.Init);
        Assert.Equal(4, u.Speed);
        Assert.Equal(87, u.SquadValue);
        Assert.Equal(110, u.Cost);
        Assert.Equal("melee_type", u.Ai);
        Assert.NotNull(u.Tags);
        Assert.Contains("human", u.Tags!);
        Assert.NotNull(u.Passives);
        Assert.Contains(u.Passives!, p => p.Name == "Double Strike");
    }

    [Fact]
    public void Units_TooltipText_IncludesCoreStatsLine()
    {
        var u = Catalog.Units.Single(x => x.Id == "esquire_upg");
        var tip = u.TooltipText();
        Assert.Contains("T1", tip);
        Assert.Contains("Guard Captain", tip);
        Assert.Contains("HP 12", tip);
        Assert.Contains("Off 6", tip);
        Assert.Contains("Def 5", tip);
        Assert.Contains("Dmg 2-3", tip);
        Assert.Contains("Init 6", tip);
        Assert.Contains("Speed 4", tip);
    }

    // ── T-603 ───────────────────────────────────────────────────────────────
    // HeroEntry / SpellEntry / SkillEntry now load the rest of the alcaras
    // payload (specId, armyScore, stats {A,D,P,K}, starter skills, army for
    // heroes; manaCost / cooldown / learnCost / icon / magicType for spells;
    // baseDesc / levels / subclasses / starters for skills). Pin one entry
    // per type so a future catalog refresh that drops any of these fields
    // fails loudly.
    [Fact]
    public void Heroes_LoadEnrichedFields_FromKnownEntry()
    {
        var h = Catalog.Heroes.Single(x => x.Id == "human_hero_10");
        Assert.Equal("Merry Elias", h.Name);
        Assert.Equal("human_hero_10_specialization", h.SpecId);
        Assert.Equal(3046, h.ArmyScore);
        Assert.NotNull(h.Stats);
        Assert.Equal(1, h.Stats!.A);
        Assert.Equal(1, h.Stats.D);
        Assert.Equal(2, h.Stats.P);
        Assert.Equal(3, h.Stats.K);
        Assert.NotNull(h.Skills);
        Assert.Contains("Righteousness L1", h.Skills!);
        Assert.NotNull(h.Army);
        Assert.Contains("Swordsman", h.Army!);
    }

    [Fact]
    public void Heroes_TooltipText_IncludesNewFields()
    {
        var h = Catalog.Heroes.Single(x => x.Id == "human_hero_10");
        var tip = h.TooltipText();
        Assert.Contains("Merry Elias", tip);
        Assert.Contains("A 1", tip);
        Assert.Contains("K 3", tip);
        Assert.Contains("Army score 3046", tip);
        Assert.Contains("Righteousness L1", tip);
    }

    [Fact]
    public void Spells_LoadEnrichedFields_FromKnownEntry()
    {
        var s = Catalog.Spells.Single(x => x.Id == "day_2_magic_sharp_edge");
        Assert.Equal("Blessing", s.Name);
        Assert.Equal("day_2_magic_sharp_edge", s.Icon);
        Assert.NotNull(s.ManaCost);
        Assert.Equal(4, s.ManaCost!.Count);
        Assert.All(s.ManaCost!, m => Assert.Equal(7, m));
        Assert.Equal(2, s.Cooldown);
        Assert.NotNull(s.LearnCost);
        Assert.Contains(s.LearnCost!, c => c.Name == "crystals" && c.Cost == 2);
        // magicType is empty string in the dump — round-tripped as-is.
        Assert.NotNull(s.MagicType);
    }

    [Fact]
    public void Spells_TooltipText_IncludesNewFields()
    {
        var s = Catalog.Spells.Single(x => x.Id == "day_2_magic_sharp_edge");
        var tip = s.TooltipText();
        Assert.Contains("T1 Blessing", tip);
        Assert.Contains("Mana 7", tip);
        Assert.Contains("CD 2", tip);
    }

    [Fact]
    public void Skills_LoadEnrichedFields_FromKnownEntry()
    {
        var sk = Catalog.Skills.Single(x => x.Id == "skill_assault");
        Assert.Equal("Offense", sk.Name);
        Assert.False(string.IsNullOrWhiteSpace(sk.BaseDesc));
        Assert.NotNull(sk.Levels);
        Assert.True(sk.Levels!.Count >= 3);
        var basic = sk.Levels!.Single(l => l.Level == 1);
        Assert.Equal("Basic Offense", basic.Name);
        Assert.NotNull(basic.Bonuses);
        Assert.NotEmpty(basic.Bonuses!);
        Assert.NotNull(sk.Subclasses);
        Assert.NotEmpty(sk.Subclasses!);
        Assert.NotNull(sk.Starters);
        Assert.NotEmpty(sk.Starters!);
    }

    [Fact]
    public void Skills_TooltipText_IncludesNewFields()
    {
        var sk = Catalog.Skills.Single(x => x.Id == "skill_assault");
        var tip = sk.TooltipText();
        Assert.Contains("Offense", tip);
        // baseDesc surfaces verbatim.
        Assert.Contains(sk.BaseDesc!, tip);
        // tier/subclass/starter counts surface as a summary line.
        Assert.Contains("tier(s)", tip);
        Assert.Contains("subclass(es)", tip);
        Assert.Contains("starter(s)", tip);
    }

    [Fact]
    public void Specializations_ContainsKnownEntry()
    {
        // "Born to Lead" is Valentina's campaign specialization, locked in
        // upstream's catalog generator output.
        var entry = Catalog.Specializations.Single(s =>
            s.Id == "campaign_hero_3_specialization");
        Assert.Equal("Born to Lead", entry.Name);
        Assert.Equal("human", entry.Faction);
        Assert.Equal("might", entry.ClassType);
        Assert.NotNull(entry.Heroes);
        Assert.Contains(entry.Heroes!, h => h.Name == "Valentina");
    }
}
