using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-501 — Connection.guardRandomization. The model must accept the value
/// when present in shipped templates (e.g. All Around.rmg.json connections),
/// must omit the field when null, and a per-template default surfaced via
/// <see cref="ConnectionDefaultsSettings.GuardRandomization"/> must overlay
/// every emitted connection.
/// </summary>
public class ConnectionGuardRandomizationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string ExampleTemplatesDir = Path.Combine(
        RepoPaths.GeneratorDataRoot(), "..", "ExampleTemplates");

    private static GeneratorSettings BaseSettings(MapTopology topology = MapTopology.Chain) => new()
    {
        TemplateName = "Conn GuardRand Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = topology,
    };

    private static IEnumerable<Connection> AllConnections(RmgTemplate t) =>
        t.Variants?.SelectMany(v => v.Connections ?? Enumerable.Empty<Connection>())
        ?? Enumerable.Empty<Connection>();

    [Fact]
    public void AllAround_GuardRandomization_ParsesAndRoundTrips()
    {
        string path = Path.Combine(ExampleTemplatesDir, "All Around.rmg.json");
        Assert.True(File.Exists(path), $"Fixture missing: {path}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        RmgTemplate template;
        using (var stream = File.OpenRead(path))
        {
            template = JsonSerializer.Deserialize<RmgTemplate>(stream, options)
                       ?? throw new InvalidOperationException("Failed to load All Around");
        }

        var conns = AllConnections(template).ToList();
        Assert.NotEmpty(conns);

        // The fixture stamps guardRandomization on every connection (values
        // 0.10–0.15). Confirm at least one matches each.
        Assert.Contains(conns, c => c.GuardRandomization == 0.15);
        Assert.Contains(conns, c => c.GuardRandomization == 0.10);

        // Round-trip: serialize back, reparse — values survive.
        string json = JsonSerializer.Serialize(template, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<RmgTemplate>(json, options)!;
        var rtConns = AllConnections(roundTripped).ToList();
        Assert.Equal(conns.Count, rtConns.Count);
        for (int i = 0; i < conns.Count; i++)
            Assert.Equal(conns[i].GuardRandomization, rtConns[i].GuardRandomization);
    }

    [Fact]
    public void Default_GuardRandomization_NotEmittedInJson()
    {
        var settings = BaseSettings();
        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Null(c.GuardRandomization));

        // Re-serialize then re-parse; the connection objects must still omit
        // guardRandomization. (Zone-level guardRandomization is a separate
        // field with its own non-null default and IS emitted — that's not
        // this feature's surface.)
        string json = JsonSerializer.Serialize(template, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<RmgTemplate>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.All(AllConnections(roundTripped), c => Assert.Null(c.GuardRandomization));
    }

    [Fact]
    public void ConnectionDefaults_GuardRandomization_StampedOnEveryConnection()
    {
        var settings = BaseSettings();
        settings.ConnectionDefaults.GuardRandomization = 0.15;

        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Equal(0.15, c.GuardRandomization));

        string json = JsonSerializer.Serialize(template, JsonOptions);
        Assert.Contains("\"guardRandomization\":0.15", json);
    }

    [Fact]
    public void ConnectionDefaults_GuardRandomization_Zero_IsExplicitOverride()
    {
        // 0.0 is a meaningful value (no randomization) distinct from "unset" (null).
        var settings = BaseSettings();
        settings.ConnectionDefaults.GuardRandomization = 0.0;

        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Equal(0.0, c.GuardRandomization));
    }

    [Fact]
    public void SettingsFile_RoundTripsGuardRandomization_ThroughMapper()
    {
        var g = BaseSettings();
        g.ConnectionDefaults.GuardRandomization = 0.12;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(0.12, back.ConnectionDefaults.GuardRandomization);
    }

    [Fact]
    public void SettingsFile_RoundTripsGuardRandomization_NullByDefault()
    {
        var g = BaseSettings();

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Null(back.ConnectionDefaults.GuardRandomization);
    }

    [Fact]
    public void SettingsFile_RoundTripsGuardRandomization_ThroughShareCodec()
    {
        var g = BaseSettings();
        g.ConnectionDefaults.GuardRandomization = 0.08;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        var (back, _, _, _) = SettingsMapper.FromFile(decoded!);

        Assert.Equal(0.08, back.ConnectionDefaults.GuardRandomization);
    }
}
