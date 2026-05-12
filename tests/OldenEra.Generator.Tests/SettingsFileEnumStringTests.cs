using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsFileEnumStringTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Topology_serialises_as_string()
    {
        var s = new SettingsFile { Topology = MapTopology.Random };
        var json = JsonSerializer.Serialize(s, Opts);
        Assert.Contains("\"topology\":\"Random\"", json);
    }

    [Fact]
    public void Topology_still_reads_legacy_int_form()
    {
        // Existing shipped .oetgs files use ints; AllowIntegerValues defaults true.
        var json = "{\"topology\":4}";
        var s = JsonSerializer.Deserialize<SettingsFile>(json, Opts)!;
        Assert.Equal(MapTopology.Random, s.Topology); // legacy int 4 == Random
    }

    [Fact]
    public void SettingsShareCodec_emits_topology_as_string_in_production_options()
    {
        var s = new SettingsFile { Topology = MapTopology.Random };
        var encoded = SettingsShareCodec.Encode(s);

        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Equal(MapTopology.Random, decoded!.Topology);
    }

    [Fact]
    public void SettingsShareCodec_decodes_legacy_int_topology_in_lenient_mode()
    {
        // Pin the LenientOptions behaviour through the production decode path:
        // a future change disabling AllowIntegerValues would break legacy
        // share-links and must fail this test.
        var legacyJson = "{\"templateName\":\"legacy\",\"topology\":4}";
        var bytes      = System.Text.Encoding.UTF8.GetBytes(legacyJson);
        using var ms   = new System.IO.MemoryStream();
        using (var gz  = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(bytes, 0, bytes.Length);
        }
        var encoded = System.Convert.ToBase64String(ms.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var back = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(back);
        Assert.Equal(MapTopology.Random, back!.Topology);
    }
}
