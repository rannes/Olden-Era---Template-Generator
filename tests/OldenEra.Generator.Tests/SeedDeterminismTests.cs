using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SeedDeterminismTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    private static GeneratorSettings BuildBaseSettings(int? seed) => new()
    {
        TemplateName = "SeedTest",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Random,
        Seed = seed,
    };

    [Fact]
    public void SameSeed_Produces_Identical_Json()
    {
        var a = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
        var b = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_Produce_Different_Json()
    {
        var a = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
        var b = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(43)), Opts);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NullSeed_Still_Generates_Without_Error()
    {
        var t = TemplateGenerator.Generate(BuildBaseSettings(null));
        Assert.NotNull(t);
    }

    /// <summary>
    /// Run the generator three times back-to-back at the same seed and
    /// confirm output is byte-identical every time. Catches non-seeded
    /// state leaks (static caches, ambient <c>new Random()</c> calls,
    /// dictionary enumeration order, etc.) that the simple two-call test
    /// might miss by chance.
    /// </summary>
    [Fact]
    public void SameSeed_RepeatedRuns_AllByteIdentical()
    {
        var hashes = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            var json = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
            hashes.Add(Sha256(json));
        }
        Assert.Single(hashes);
    }

    public static TheoryData<string> AllShippedPresetIds()
    {
        var data = new TheoryData<string>();
        foreach (var entry in new PresetCatalog().Entries)
            data.Add(entry.Id);
        return data;
    }

    /// <summary>
    /// Each shipped preset must be deterministic under a fixed seed. This
    /// is the cross-preset acceptance test for T-801: any preset that
    /// reaches a non-seeded RNG path will diverge on rerun.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllShippedPresetIds))]
    public void EveryPreset_SameSeed_Produces_Identical_Json(string presetId)
    {
        var catalog = new PresetCatalog();

        var (settingsA, _, _, _) = SettingsMapper.FromFile(catalog.Load(presetId));
        settingsA.Seed = 12345;
        var (settingsB, _, _, _) = SettingsMapper.FromFile(catalog.Load(presetId));
        settingsB.Seed = 12345;

        var a = JsonSerializer.Serialize(TemplateGenerator.Generate(settingsA), Opts);
        var b = JsonSerializer.Serialize(TemplateGenerator.Generate(settingsB), Opts);

        Assert.Equal(a, b);
    }

    /// <summary>
    /// Pin a known-good output hash for a fixed (preset, seed) pair so we
    /// notice if generator changes silently shift the deterministic
    /// output. The expected hash is the SHA-256 of the serialized
    /// template captured the first time the test runs locally; if the
    /// generator's deterministic output legitimately changes the
    /// expected hash here must be updated in the same commit. That's the
    /// signal that the change touches map output, which deserves an
    /// explicit reviewer ack.
    /// </summary>
    [Fact]
    public void FixedSeed_Preset_RegressionHash_IsStable()
    {
        var catalog = new PresetCatalog();
        var (settings, _, _, _) = SettingsMapper.FromFile(catalog.Load("six-kings"));
        settings.Seed = 20260515;

        // Pinned 2026-05-15 against the current generator output. If this
        // assertion fails after a generator change, run the test, copy the
        // actual hash here, and call out the deterministic-output shift in
        // the PR description.
        const string ExpectedHash =
            "839C9C48BD0C828CB8B150D0DA629C72D205E564D003B69D26B9D70DE28FB0E1";

        var json = JsonSerializer.Serialize(TemplateGenerator.Generate(settings), Opts);
        var hash = Sha256(json);
        Assert.Equal(ExpectedHash, hash);

        // Re-run to confirm the hash is itself reproducible (the test would
        // be useless otherwise).
        var json2 = JsonSerializer.Serialize(TemplateGenerator.Generate(settings), Opts);
        Assert.Equal(hash, Sha256(json2));
    }

    private static string Sha256(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest);
    }
}
