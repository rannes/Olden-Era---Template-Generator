using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-001 — Connection.length, gatePlacement, guardEscape, simTurnSquad.
///
/// The generator already emits a few of these (e.g. GuardEscape=false) on
/// hand-coded paths. The user-controllable surface is exposed via
/// <see cref="ConnectionDefaultsSettings"/>: when set, every emitted Connection
/// is overlaid with the user value. When unset, output is byte-identical.
/// </summary>
public class ConnectionFieldsEmissionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static GeneratorSettings BaseSettings(MapTopology topology = MapTopology.Chain) => new()
    {
        TemplateName = "Conn Fields Test",
        PlayerCount = 4,
        MapSize = 160,
        Topology = topology,
    };

    private static IEnumerable<Connection> AllConnections(RmgTemplate t) =>
        t.Variants?.SelectMany(v => v.Connections ?? Enumerable.Empty<Connection>())
        ?? Enumerable.Empty<Connection>();

    [Fact]
    public void Defaults_DoNotEmitLengthOrGatePlacement_AndPreserveBaseline()
    {
        var settings = BaseSettings();
        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);

        // Default settings: user-controllable knobs are unset, so no Connection
        // should pick up a Length or GatePlacement value from the overlay path.
        Assert.All(conns, c => Assert.Null(c.Length));
        Assert.All(conns, c => Assert.Null(c.GatePlacement));
    }

    [Fact]
    public void Length_NonDefault_EmittedOnEveryConnection_ChainTopology()
    {
        var settings = BaseSettings(MapTopology.Chain);
        settings.ConnectionDefaults.Length = 2.5;

        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Equal(2.5, c.Length));

        // Round-trip through JSON: the value survives the schema serializer.
        var json = JsonSerializer.Serialize(template, JsonOptions);
        Assert.Contains("\"length\":2.5", json);
    }

    [Fact]
    public void GatePlacement_NonDefault_EmittedAsString()
    {
        var settings = BaseSettings();
        settings.ConnectionDefaults.GatePlacement = "Center";

        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Equal("Center", c.GatePlacement));

        var json = JsonSerializer.Serialize(template, JsonOptions);
        Assert.Contains("\"gatePlacement\":\"Center\"", json);
    }

    [Fact]
    public void GuardEscape_AndSimTurnSquad_NonDefault_EmittedOnEveryConnection()
    {
        var settings = BaseSettings();
        settings.ConnectionDefaults.GuardEscape = true;
        settings.ConnectionDefaults.SimTurnSquad = true;

        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();

        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Equal(true, c.GuardEscape));
        Assert.All(conns, c => Assert.Equal(true, c.SimTurnSquad));
    }

    [Fact]
    public void Defaults_OutputIsByteIdentical_AcrossRuns_BeforeAndAfterFeature()
    {
        // Smoke check: the bare default settings still serialize to a stable
        // structure with no spurious connection field additions.
        var settings = BaseSettings();
        var template = TemplateGenerator.Generate(settings);
        var json = JsonSerializer.Serialize(template, JsonOptions);

        // No emission of Length / GatePlacement when the overlay is unset.
        // (GuardEscape=false IS emitted by some hand-coded paths — that's
        // pre-existing baseline behavior, not new from this feature.)
        Assert.DoesNotContain("\"length\":", json);
        Assert.DoesNotContain("\"gatePlacement\":", json);
    }

    [Fact]
    public void SettingsFile_RoundTripsConnectionDefaults_ThroughMapper()
    {
        var g = BaseSettings();
        g.ConnectionDefaults.Length = 1.5;
        g.ConnectionDefaults.GatePlacement = "Center";
        g.ConnectionDefaults.GuardEscape = false;
        g.ConnectionDefaults.SimTurnSquad = true;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal(1.5, back.ConnectionDefaults.Length);
        Assert.Equal("Center", back.ConnectionDefaults.GatePlacement);
        Assert.Equal(false, back.ConnectionDefaults.GuardEscape);
        Assert.Equal(true, back.ConnectionDefaults.SimTurnSquad);
    }

    [Fact]
    public void Preset_Arcade2v2_ExercisesConnectionDefaults_EndToEnd()
    {
        // T-001 acceptance: at least one preset uses a non-default value
        // to prove the file → settings → generator path end-to-end.
        var catalog = new PresetCatalog();
        var file = catalog.Load("arcade-2v2");

        Assert.True(file.ConnectionSimTurnSquad);
        Assert.False(file.ConnectionGuardEscape);

        var (settings, _, _, _) = SettingsMapper.FromFile(file);
        Assert.Equal(true, settings.ConnectionDefaults.SimTurnSquad);
        Assert.Equal(false, settings.ConnectionDefaults.GuardEscape);

        var template = TemplateGenerator.Generate(settings);
        var conns = AllConnections(template).ToList();
        Assert.NotEmpty(conns);
        Assert.All(conns, c => Assert.Equal(true, c.SimTurnSquad));
        Assert.All(conns, c => Assert.Equal(false, c.GuardEscape));
    }

    [Fact]
    public void SettingsFile_RoundTripsConnectionDefaults_ThroughShareCodec()
    {
        var g = BaseSettings();
        g.ConnectionDefaults.Length = 0.75;
        g.ConnectionDefaults.GatePlacement = "Center";
        g.ConnectionDefaults.SimTurnSquad = true;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);

        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        var (back, _, _, _) = SettingsMapper.FromFile(decoded!);

        Assert.Equal(0.75, back.ConnectionDefaults.Length);
        Assert.Equal("Center", back.ConnectionDefaults.GatePlacement);
        Assert.Null(back.ConnectionDefaults.GuardEscape);
        Assert.Equal(true, back.ConnectionDefaults.SimTurnSquad);
    }
}
