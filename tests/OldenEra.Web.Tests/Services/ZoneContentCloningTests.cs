using System;
using System.Collections.Generic;
using System.Reflection;
using OldenEra.Generator.Models;
using OldenEra.Web.Services;

namespace OldenEra.Web.Tests.Services;

public class ZoneContentCloningTests
{
    [Fact]
    public void CloneList_ReturnsIndependentItems()
    {
        var original = new ZoneContentList();
        original.Items.Add(new ZoneContentItem { Sid = "x", Handle = "h", MinCount = 2, MaxCount = 3 });

        var clone = ZoneContentCloning.CloneList(original);
        clone.Items[0].Sid = "MUTATED";
        clone.Items[0].FactionAffinity.Add("haven");

        Assert.Equal("x", original.Items[0].Sid);
        Assert.Empty(original.Items[0].FactionAffinity);
        Assert.NotSame(original.Items, clone.Items);
        Assert.NotSame(original.Items[0], clone.Items[0]);
    }

    [Fact]
    public void CloneNeutral_ProducesIndependentTierAndZoneDictionaries()
    {
        var original = new NeutralZoneContent();
        original.Global.Items.Add(new ZoneContentItem { Sid = "g" });
        original.ByTier[NeutralZoneTier.Normal] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "t" } },
        };
        original.ByZoneLetter["A"] = new ZoneContentList
        {
            Items = { new ZoneContentItem { Sid = "z" } },
        };

        var clone = ZoneContentCloning.CloneNeutral(original);
        clone.Global.Items.Clear();
        clone.ByTier[NeutralZoneTier.Normal].Items.Clear();
        clone.ByZoneLetter["A"].Items.Clear();
        clone.ByZoneLetter["B"] = new ZoneContentList();

        Assert.Single(original.Global.Items);
        Assert.Single(original.ByTier[NeutralZoneTier.Normal].Items);
        Assert.Single(original.ByZoneLetter["A"].Items);
        Assert.False(original.ByZoneLetter.ContainsKey("B"));
    }

    [Fact]
    public void CloneRoadDecorations_IsIndependent()
    {
        var original = new List<ZoneRoadDecoration>
        {
            new() { Zone = "A", RoadType = ZoneRoadType.Stone },
        };

        var clone = ZoneContentCloning.CloneRoadDecorations(original);
        clone[0].Zone = "Z";
        clone.Add(new ZoneRoadDecoration());

        Assert.Equal("A", original[0].Zone);
        Assert.Single(original);
    }

    [Fact]
    public void CloneWithDefaultsBlanked_PreservesNonZoneContent_AndBlanksZoneTrees()
    {
        var settings = new GeneratorSettings { Seed = 42 };
        settings.PlayerZoneContent.Items.Add(new ZoneContentItem { Sid = "p" });
        settings.NeutralZoneContent.Global.Items.Add(new ZoneContentItem { Sid = "n" });
        settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration { Zone = "B" });

        var clone = ZoneContentCloning.CloneWithDefaultsBlanked(settings);

        Assert.Equal(42, clone.Seed);
        Assert.Empty(clone.PlayerZoneContent.Items);
        Assert.Empty(clone.NeutralZoneContent.Global.Items);
        Assert.Empty(clone.NeutralZoneContent.ByTier);
        Assert.Empty(clone.NeutralZoneContent.ByZoneLetter);
        Assert.Empty(clone.ZoneRoadDecorations);

        // and the source is untouched:
        Assert.Single(settings.PlayerZoneContent.Items);
        Assert.Single(settings.NeutralZoneContent.Global.Items);
        Assert.Single(settings.ZoneRoadDecorations);
    }

    /// <summary>
    /// Drift guard: if a future round adds a property to <see cref="ZoneContentItem"/>
    /// without updating <see cref="ZoneContentCloning.CloneItem"/>, this test fails
    /// loudly instead of silently producing stale clones.
    /// </summary>
    [Fact]
    public void CloneItem_CopiesAllPublicProperties_AndDoesNotAliasReferenceTypeMembers()
    {
        var props = typeof(ZoneContentItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var original = new ZoneContentItem();
        var listProps = new List<PropertyInfo>();

        foreach (var prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var t = prop.PropertyType;
            object value;

            if (t == typeof(string))
            {
                value = "v_" + prop.Name;
            }
            else if (t == typeof(int))
            {
                value = 7;
            }
            else if (t == typeof(bool))
            {
                value = true;
            }
            else if (t.IsEnum)
            {
                var values = Enum.GetValues(t);
                Assert.True(values.Length >= 2,
                    $"Enum {t.Name} needs >= 2 values for drift test to pick a non-default.");
                value = values.GetValue(1)!;
            }
            else if (Nullable.GetUnderlyingType(t) is { IsEnum: true } underlying)
            {
                var values = Enum.GetValues(underlying);
                Assert.True(values.Length >= 1,
                    $"Enum {underlying.Name} needs >= 1 value for drift test.");
                value = values.GetValue(0)!;
            }
            else if (t == typeof(List<string>))
            {
                value = new List<string> { "item_" + prop.Name };
                listProps.Add(prop);
            }
            else
            {
                Assert.Fail(
                    $"Add coverage for property {prop.Name} of type {t.FullName} " +
                    "to CloneItem drift test.");
                return;
            }

            prop.SetValue(original, value);
        }

        var clone = ZoneContentCloning.CloneItem(original);

        foreach (var prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var originalValue = prop.GetValue(original);
            var cloneValue = prop.GetValue(clone);

            Assert.Equal(originalValue, cloneValue);
        }

        foreach (var prop in listProps)
        {
            var originalList = (List<string>)prop.GetValue(original)!;
            var cloneList = (List<string>)prop.GetValue(clone)!;

            Assert.NotSame(originalList, cloneList);

            cloneList.Add("mutation_check");
            Assert.DoesNotContain("mutation_check", originalList);
        }
    }
}
