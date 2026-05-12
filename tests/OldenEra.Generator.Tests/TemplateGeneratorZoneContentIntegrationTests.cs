using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class TemplateGeneratorZoneContentIntegrationTests
{
    [Fact]
    public void Generate_AppendsUserMandatoryItems_ToPlayerSpawnGroup()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1 },
            Topology = MapTopology.Default,
            Seed = 1,
        };

        settings.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well",
            Handle = "user_well",
            Pool = ZoneContentPool.Mandatory,
        });

        var template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.MandatoryContent);
        var anySpawnGroupHasUserWell = template.MandatoryContent!
            .Where(g => g.Name != null && g.Name.StartsWith("mandatory_content_side_", StringComparison.Ordinal))
            .Any(g => g.Content != null && g.Content.Any(c => c.Name == "user_well" && c.Sid == "mana_well"));

        Assert.True(anySpawnGroupHasUserWell);
    }

    [Fact]
    public void Generate_AppendsUserMandatoryItems_ToNeutralGroup_FromGlobalLayer()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1 },
            Topology = MapTopology.Default,
            Seed = 1,
        };

        settings.NeutralZoneContent.Global.Items.Add(new ZoneContentItem
        {
            Sid = "watchtower",
            Handle = "user_tower",
            Pool = ZoneContentPool.Mandatory,
        });

        var template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.MandatoryContent);
        var anyNeutralGroupHasUserTower = template.MandatoryContent!
            .Where(g => g.Name != null && g.Name.StartsWith("mandatory_content_neutral_", StringComparison.Ordinal))
            .Any(g => g.Content != null && g.Content.Any(c => c.Name == "user_tower" && c.Sid == "watchtower"));

        Assert.True(anyNeutralGroupHasUserTower);
    }

    [Fact]
    public void Generate_AppendsUserRoadDecoration_ToTargetZone()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1 },
            Topology = MapTopology.Default,
            Seed = 1,
        };

        settings.PlayerZoneContent.Items.Add(new ZoneContentItem
        {
            Sid = "mana_well",
            Handle = "user_well",
            Pool = ZoneContentPool.Mandatory,
        });

        settings.ZoneRoadDecorations.Add(new ZoneRoadDecoration
        {
            Zone = "Spawn-A",
            RoadType = "Stone",
            From = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MainObject, Arg = "0" },
            To = new ZoneRoadEndpoint { Kind = ZoneRoadEndpointKind.MandatoryContent, Arg = "user_well" },
        });

        var template = TemplateGenerator.Generate(settings);

        var spawnZone = template.Variants!.Single().Zones!.Single(z => z.Name == "Spawn-A");
        Assert.NotNull(spawnZone.Roads);
        Assert.Contains(spawnZone.Roads!, r =>
            r.Type == "Stone" &&
            r.From!.Type == "MainObject" && r.From.Args![0] == "0" &&
            r.To!.Type == "MandatoryContent" && r.To.Args![0] == "user_well");
    }

    [Fact]
    public void Generate_EmptyUserInputs_ProduceNoUserSignatureInOutput()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1 },
            Topology = MapTopology.Default,
            Seed = 1,
        };

        Assert.Empty(settings.PlayerZoneContent.Items);
        Assert.Empty(settings.NeutralZoneContent.Global.Items);
        Assert.Empty(settings.NeutralZoneContent.ByTier);
        Assert.Empty(settings.NeutralZoneContent.ByZoneLetter);
        Assert.Empty(settings.ZoneRoadDecorations);

        var template = TemplateGenerator.Generate(settings);

        // No mandatory item carries a user-authored Name (auto-name or otherwise).
        foreach (var group in template.MandatoryContent ?? new())
        {
            var content = group.Content ?? new();
            foreach (var item in content)
            {
                // The auto-name format is "name_user_<zone>_<sid>_<index>".
                // Any item with that prefix would mean the emitter ran on empty input.
                Assert.False(
                    item.Name?.StartsWith("name_user_", StringComparison.Ordinal) == true,
                    $"Found user-signature Name '{item.Name}' in group '{group.Name}'.");
            }
        }

        // No zone carries a road whose endpoint references a user-authored MandatoryContent name.
        var variant = template.Variants?.Single();
        foreach (var zone in variant?.Zones ?? new())
        {
            foreach (var road in zone.Roads ?? new())
            {
                foreach (var endpoint in new[] { road.From, road.To })
                {
                    if (endpoint?.Type == "MandatoryContent")
                    {
                        var arg = endpoint.Args?.FirstOrDefault();
                        Assert.False(
                            arg?.StartsWith("name_user_", StringComparison.Ordinal) == true,
                            $"Found user-signature road endpoint arg '{arg}' on zone '{zone.Name}'.");
                    }
                }
            }
        }
    }
}
