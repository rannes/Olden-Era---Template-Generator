using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsMapperZoneContentRoundTripTests
{
    private static GeneratorSettings BuildPopulated()
    {
        var g = new GeneratorSettings { TemplateName = "round3" };
        g.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well", Handle = "h", IsGroup = false,
            MinCount = 1, MaxCount = 2, Pool = ZoneContentPool.Mandatory,
            IsGuarded = true, NearCastle = true,
            RoadDistance = RoadDistance.Mid,
            FactionAffinity = new() { "haven" },
            BiomeFilter = new() { "grass" },
            IncludeListIds = new() { "basic_content_list_pickup_random_items" },
            Rules = new()
            {
                new ZoneContentRule { Type = "Crossroads", TargetMin = 0.10, TargetMax = 0.30, Weight = 1 },
                new ZoneContentRule { Type = "MainObject", Args = new() { "0" }, TargetMin = 0.05, TargetMax = 0.25, Weight = 2 },
            },
        });
        // Use Global.Items for NeutralZoneContent (flat .Items doesn't exist).
        g.NeutralZoneContent.Global.Items.Add(new ZoneContentItem
        {
            Sid = "pandora_box", IsGroup = true, MinCount = 1, MaxCount = 3,
            Pool = ZoneContentPool.Guarded, IsGuarded = false, NearCastle = false,
            RoadDistance = RoadDistance.Far,
            FactionAffinity = new() { "necro" },
            BiomeFilter = new() { "snow" },
        });
        g.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "side_a",
            RoadType = ZoneRoadType.Dirt,
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "h" },
            To   = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.Connection,       Arg = "side_a-side_b-1" },
        });
        return g;
    }

    [Fact]
    public void Mapper_round_trips_zone_content_surfaces()
    {
        var original = BuildPopulated();
        var file = SettingsMapper.ToFile(original, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Single(back.PlayerZoneContent.Items);
        Assert.Equal("h", back.PlayerZoneContent.Items[0].Handle);
        Assert.Equal(RoadDistance.Mid, back.PlayerZoneContent.Items[0].RoadDistance);

        Assert.Single(back.NeutralZoneContent.Global.Items);
        Assert.Equal(3, back.NeutralZoneContent.Global.Items[0].MaxCount);

        Assert.Single(back.ZoneRoadDecorations);
        Assert.Equal(ZoneRoadType.Dirt, back.ZoneRoadDecorations[0].RoadType);

        // Deep round-trip: every public property on every nested DTO must survive
        // the FromFile/ToFile transit. Compare via JSON to avoid needing IEquatable.
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(original.PlayerZoneContent, opts),
            System.Text.Json.JsonSerializer.Serialize(back.PlayerZoneContent, opts));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(original.NeutralZoneContent, opts),
            System.Text.Json.JsonSerializer.Serialize(back.NeutralZoneContent, opts));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(original.ZoneRoadDecorations, opts),
            System.Text.Json.JsonSerializer.Serialize(back.ZoneRoadDecorations, opts));
    }

    /// <summary>
    /// Reflection guard: for each public property on the new DTO types,
    /// at least one fixture instance must be non-default. Future-added
    /// properties fail this until the fixture is updated, forcing coverage.
    /// </summary>
    [Theory]
    [InlineData(typeof(ZoneContentItem))]
    [InlineData(typeof(ZoneRoadDecoration))]
    [InlineData(typeof(ZoneRoadEndpoint))]
    public void Fixture_populates_every_public_property_in_at_least_one_instance(Type type)
    {
        var g = BuildPopulated();
        IEnumerable<object> instances = type switch
        {
            _ when type == typeof(ZoneContentItem) =>
                g.PlayerZoneContent.Items.Cast<object>()
                 .Concat(g.NeutralZoneContent.Global.Items.Cast<object>()),
            _ when type == typeof(ZoneRoadDecoration) => g.ZoneRoadDecorations.Cast<object>(),
            _ when type == typeof(ZoneRoadEndpoint) =>
                g.ZoneRoadDecorations.SelectMany(d => new object[] { d.From, d.To }),
            _ => throw new ArgumentException(null, nameof(type)),
        };
        var allInstances = instances.ToList();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            bool anyNonDefault = allInstances.Any(inst => !IsDefault(prop.GetValue(inst), prop.PropertyType));
            Assert.True(anyNonDefault,
                $"No fixture instance has a non-default {type.Name}.{prop.Name}; populate one.");
        }
    }

    private static bool IsDefault(object? value, Type type)
    {
        if (value is null) return true;
        if (value is string s) return string.IsNullOrEmpty(s);
        if (value is System.Collections.ICollection c) return c.Count == 0;
        if (value is bool b) return !b;
        if (value is int i) return i == 0;
        if (value is Enum e) return Convert.ToInt32(e) == 0;
        // Fallback: equality with default(T)
        var defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
        return Equals(value, defaultValue);
    }
}
