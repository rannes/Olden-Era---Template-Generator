using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-206 — per-player overrides on starting bonuses.
///
/// The emitter must:
///  - Stay byte-identical when the override list is empty.
///  - When any override touches a field, emit per-slot rows for that field only,
///    using the override value where provided and the uniform value elsewhere.
///  - Skip rows whose PlayerSlot falls outside 1..PlayerCount.
/// </summary>
public class PerPlayerBonusesTests
{
    private static GeneratorSettings BaseSettings() => new()
    {
        TemplateName = "Per-Player Bonus Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Chain,
    };

    private static IEnumerable<Bonus> Bonuses(RmgTemplate t) =>
        t.GameRules?.Bonuses ?? Enumerable.Empty<Bonus>();

    [Fact]
    public void Defaults_NoPerPlayerOverrides_NoSlotSpecificBonuses()
    {
        var s = BaseSettings();
        var t = TemplateGenerator.Generate(s);
        // No overrides, no uniform bonuses set — only the generator's stock bonuses
        // (movement etc. with ReceiverSide -1) should appear.
        Assert.DoesNotContain(Bonuses(t), b => b.ReceiverSide is int side && side >= 1);
    }

    [Fact]
    public void UniformOnly_HeroAttack_EmitsSingleAllPlayersRow()
    {
        var s = BaseSettings();
        s.Bonuses.HeroAttack = 2;
        var t = TemplateGenerator.Generate(s);
        var stat = Bonuses(t).Where(b => b.Sid == "add_bonus_hero_stat"
                                          && b.Parameters?.Count == 2
                                          && b.Parameters[0] == "attack").ToList();
        Assert.Single(stat);
        Assert.Equal(-1, stat[0].ReceiverSide);
        Assert.Equal("2", stat[0].Parameters![1]);
    }

    [Fact]
    public void Override_HeroAttack_EmitsPerSlotRowsForThatField()
    {
        var s = BaseSettings();
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings { HeroAttack = 5 },
        });

        var t = TemplateGenerator.Generate(s);
        var attack = Bonuses(t).Where(b => b.Sid == "add_bonus_hero_stat"
                                            && b.Parameters?.Count == 2
                                            && b.Parameters[0] == "attack").ToList();

        // No ReceiverSide=-1 row for attack — overrides force per-slot expansion.
        Assert.DoesNotContain(attack, b => b.ReceiverSide == -1);
        Assert.Equal(4, attack.Count);
        Assert.Equal("2", attack.Single(b => b.ReceiverSide == 1).Parameters![1]);
        Assert.Equal("5", attack.Single(b => b.ReceiverSide == 2).Parameters![1]);
        Assert.Equal("2", attack.Single(b => b.ReceiverSide == 3).Parameters![1]);
        Assert.Equal("2", attack.Single(b => b.ReceiverSide == 4).Parameters![1]);
    }

    [Fact]
    public void Override_DoesNotAffectOtherFields()
    {
        var s = BaseSettings();
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.Resources["gold"] = 1000;
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings { HeroAttack = 5 },
        });

        var t = TemplateGenerator.Generate(s);
        var gold = Bonuses(t).Where(b => b.Sid == "add_bonus_res"
                                          && b.Parameters?.Count == 2
                                          && b.Parameters[0] == "gold").ToList();
        Assert.Single(gold);
        Assert.Equal(-1, gold[0].ReceiverSide);
        Assert.Equal("1000", gold[0].Parameters![1]);
    }

    [Fact]
    public void Override_OutOfRangeSlot_IsSkipped()
    {
        var s = BaseSettings();
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 99,
            Bonuses = new StartingBonusSettings { HeroAttack = 5 },
        });

        var t = TemplateGenerator.Generate(s);
        // Out-of-range row is dropped; the field has no in-range overrides, so the
        // emitter falls back to the single uniform row.
        var attack = Bonuses(t).Where(b => b.Sid == "add_bonus_hero_stat"
                                            && b.Parameters?.Count == 2
                                            && b.Parameters[0] == "attack").ToList();
        Assert.Single(attack);
        Assert.Equal(-1, attack[0].ReceiverSide);
    }

    [Fact]
    public void Override_DuplicateSlot_LastWriteWins()
    {
        var s = BaseSettings();
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings { HeroAttack = 5 },
        });
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings { HeroAttack = 7 },
        });

        var t = TemplateGenerator.Generate(s);
        var slot2 = Bonuses(t).Single(b => b.Sid == "add_bonus_hero_stat"
                                           && b.ReceiverSide == 2
                                           && b.Parameters?[0] == "attack");
        Assert.Equal("7", slot2.Parameters![1]);
    }

    [Fact]
    public void Override_Resource_AddedSidEmitsPerSlot()
    {
        var s = BaseSettings();
        // Uniform has no gold; only slot 1 gets gold via override.
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 1,
            Bonuses = new StartingBonusSettings { Resources = { ["gold"] = 500 } },
        });

        var t = TemplateGenerator.Generate(s);
        var gold = Bonuses(t).Where(b => b.Sid == "add_bonus_res"
                                          && b.Parameters?[0] == "gold").ToList();
        // Only slot 1 has a non-zero amount; other slots are skipped (zero amount).
        Assert.Single(gold);
        Assert.Equal(1, gold[0].ReceiverSide);
        Assert.Equal("500", gold[0].Parameters![1]);
    }

    [Fact]
    public void Override_SinglePlayerCount_StillExpands()
    {
        // Edge case: with PlayerCount=1, an override on slot 1 must still emit
        // a slot-1 row (not a uniform ReceiverSide=-1 row) for the touched field.
        var s = BaseSettings();
        s.PlayerCount = 1;
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 1,
            Bonuses = new StartingBonusSettings { HeroAttack = 9 },
        });
        var t = TemplateGenerator.Generate(s);
        var attack = Bonuses(t).Where(b => b.Sid == "add_bonus_hero_stat"
                                            && b.Parameters?[0] == "attack").ToList();
        Assert.Single(attack);
        Assert.Equal(1, attack[0].ReceiverSide);
        Assert.Equal("9", attack[0].Parameters![1]);
    }

    [Fact]
    public void Validator_OutOfRangeSlot_Warns()
    {
        var s = BaseSettings();
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 99,
            Bonuses = new StartingBonusSettings { HeroAttack = 5 },
        });
        var result = SettingsValidator.Validate(s);
        Assert.True(result.IsValid); // warning, not blocker
        Assert.Contains(result.Issues,
            i => i.FieldKey == ValidationFieldKeys.BonusPerPlayerOverrides
                 && i.Severity == SettingsValidator.Severity.Warning
                 && i.Message.Contains("99"));
    }

    [Fact]
    public void Validator_DuplicateSlot_Warns()
    {
        var s = BaseSettings();
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings { HeroAttack = 5 },
        });
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings { HeroAttack = 7 },
        });
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues,
            i => i.FieldKey == ValidationFieldKeys.BonusPerPlayerOverrides
                 && i.Severity == SettingsValidator.Severity.Warning
                 && i.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Validator_EmptyRow_Warns()
    {
        var s = BaseSettings();
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 1,
            Bonuses = new StartingBonusSettings(),
        });
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Issues,
            i => i.FieldKey == ValidationFieldKeys.BonusPerPlayerOverrides
                 && i.Severity == SettingsValidator.Severity.Warning
                 && i.Message.Contains("no fields"));
    }

    [Fact]
    public void RoundTrip_SettingsFile_PreservesPerPlayerOverrides()
    {
        var g = BaseSettings();
        g.Bonuses.HeroAttack = 2;
        g.Bonuses.Resources["gold"] = 1000;
        g.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 3,
            Bonuses = new StartingBonusSettings
            {
                HeroAttack = 7,
                Resources = { ["wood"] = 25 },
                ItemSid = "art_potion_of_wisdom",
                ItemStartHeroOnly = true,
            },
        });

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var roundTripped = SettingsMapper.FromFile(file).Settings;

        Assert.Single(roundTripped.Bonuses.PerPlayerOverrides);
        var row = roundTripped.Bonuses.PerPlayerOverrides[0];
        Assert.Equal(3, row.PlayerSlot);
        Assert.Equal(7, row.Bonuses.HeroAttack);
        Assert.Equal(25, row.Bonuses.Resources["wood"]);
        Assert.Equal("art_potion_of_wisdom", row.Bonuses.ItemSid);
        Assert.True(row.Bonuses.ItemStartHeroOnly);
    }

    /// <summary>
    /// Exhaustive round-trip: populate every field on the override row so a typo
    /// in any one of the twelve mapping pairs (SettingsMapper to/from-file plus
    /// the WPF MainWindow gather/apply) would surface as a failed assertion.
    /// </summary>
    [Fact]
    public void RoundTrip_SettingsFile_PreservesAllOverrideFields()
    {
        var g = BaseSettings();
        g.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings
            {
                Resources = { ["gold"] = 500, ["wood"] = 25, ["mercury"] = 3 },
                HeroAttack = 7,
                HeroDefense = 6,
                HeroSpellpower = 5,
                HeroKnowledge = 4,
                HeroStatStartHeroOnly = true,
                ItemSid = "art_potion_of_wisdom",
                ItemStartHeroOnly = true,
                SpellSid = "spell_fireball",
                SpellStartHeroOnly = true,
                UnitMultiplier = 1.25,
                UnitMultiplierStartHeroOnly = true,
            },
        });

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var roundTripped = SettingsMapper.FromFile(file).Settings;

        Assert.Single(roundTripped.Bonuses.PerPlayerOverrides);
        var row = roundTripped.Bonuses.PerPlayerOverrides[0];
        Assert.Equal(2, row.PlayerSlot);
        Assert.Equal(500, row.Bonuses.Resources["gold"]);
        Assert.Equal(25, row.Bonuses.Resources["wood"]);
        Assert.Equal(3, row.Bonuses.Resources["mercury"]);
        Assert.Equal(7, row.Bonuses.HeroAttack);
        Assert.Equal(6, row.Bonuses.HeroDefense);
        Assert.Equal(5, row.Bonuses.HeroSpellpower);
        Assert.Equal(4, row.Bonuses.HeroKnowledge);
        Assert.True(row.Bonuses.HeroStatStartHeroOnly);
        Assert.Equal("art_potion_of_wisdom", row.Bonuses.ItemSid);
        Assert.True(row.Bonuses.ItemStartHeroOnly);
        Assert.Equal("spell_fireball", row.Bonuses.SpellSid);
        Assert.True(row.Bonuses.SpellStartHeroOnly);
        Assert.Equal(1.25, row.Bonuses.UnitMultiplier);
        Assert.True(row.Bonuses.UnitMultiplierStartHeroOnly);
    }

    /// <summary>
    /// When overrides exist, the union-walk path emits uniform-only Resources
    /// in their original order, then per-slot Resources for fields any override
    /// touches, then any override-only sids. Pins the contract so a future
    /// refactor of the Resources merge logic can't silently reorder output.
    /// </summary>
    [Fact]
    public void Override_ResourceUnionWalk_PreservesOrdering()
    {
        var s = BaseSettings();
        s.Bonuses.Resources["gold"] = 1000;
        s.Bonuses.Resources["wood"] = 50;
        s.Bonuses.Resources["ore"] = 25;
        // Slot 2 diverges on wood and adds an override-only sid (mercury).
        s.Bonuses.PerPlayerOverrides.Add(new PerPlayerBonusOverride
        {
            PlayerSlot = 2,
            Bonuses = new StartingBonusSettings
            {
                Resources = { ["wood"] = 100, ["mercury"] = 3 },
            },
        });

        var t = TemplateGenerator.Generate(s);
        var resources = Bonuses(t)
            .Where(b => b.Sid == "add_bonus_res")
            .Select(b => (sid: b.Parameters![0], side: b.ReceiverSide ?? -999, amount: b.Parameters[1]))
            .ToList();

        // Expected emission order: gold (uniform), wood per-slot ×4, ore (uniform),
        // mercury per-slot for slots that have it. gold/ore stay uniform because
        // no override touches them; wood expands because slot 2 diverges; mercury
        // only emits for slot 2 (other slots have amount 0 and are skipped).
        var sids = resources.Select(r => r.sid).ToList();
        int goldIdx = sids.IndexOf("gold");
        int firstWoodIdx = sids.IndexOf("wood");
        int oreIdx = sids.IndexOf("ore");
        int mercuryIdx = sids.IndexOf("mercury");
        Assert.True(goldIdx >= 0, "gold present");
        Assert.True(firstWoodIdx > goldIdx, "wood after gold");
        Assert.True(oreIdx > firstWoodIdx, "ore after all wood rows");
        Assert.True(mercuryIdx > oreIdx, "override-only mercury emits after uniform sids");

        // Uniform sids stay single-row (ReceiverSide=-1).
        Assert.Single(resources, r => r.sid == "gold");
        Assert.Equal(-1, resources.Single(r => r.sid == "gold").side);
        Assert.Single(resources, r => r.sid == "ore");
        Assert.Equal(-1, resources.Single(r => r.sid == "ore").side);

        // Wood expands per-slot with override at slot 2.
        var wood = resources.Where(r => r.sid == "wood").ToList();
        Assert.Equal(4, wood.Count);
        Assert.Equal("50", wood.Single(r => r.side == 1).amount);
        Assert.Equal("100", wood.Single(r => r.side == 2).amount);
        Assert.Equal("50", wood.Single(r => r.side == 3).amount);
        Assert.Equal("50", wood.Single(r => r.side == 4).amount);

        // Mercury: only slot 2 has a non-zero amount, so other slots are skipped.
        var mercury = resources.Where(r => r.sid == "mercury").ToList();
        Assert.Single(mercury);
        Assert.Equal(2, mercury[0].side);
        Assert.Equal("3", mercury[0].amount);
    }
}
