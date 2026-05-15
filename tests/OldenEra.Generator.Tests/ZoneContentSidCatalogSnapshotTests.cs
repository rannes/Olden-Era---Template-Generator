using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OldenEra.Generator.Services.ZoneContent;
using Xunit;

namespace OldenEra.Generator.Tests;

/// <summary>
/// T-606 (review follow-up): pin the exact SID order produced by
/// <see cref="ZoneContentSidCatalog.All"/>. T-606 refactored the catalog
/// from a hand-listed array to <c>Enumerable.Range</c>-based codegen; the
/// agent verified byte-identity empirically but did not commit a snapshot.
/// This test computes a SHA-256 over the newline-joined SID sequence and
/// asserts the digest matches the value pinned below.
///
/// <para>
/// Any intentional catalog change (adding, removing, or reordering an SID)
/// requires updating <see cref="ExpectedSidSequenceSha256"/> in the same
/// commit. To regenerate: replace the constant with a placeholder, run
/// the test, and copy the actual hex string from the failure message.
/// </para>
/// </summary>
public class ZoneContentSidCatalogSnapshotTests
{
    /// <summary>
    /// SHA-256 (lowercase hex) of <c>string.Join("\n", All().Select(e => e.Sid))</c>.
    /// Update this constant whenever the catalog intentionally changes.
    /// </summary>
    private const string ExpectedSidSequenceSha256 =
        "20d53813d33ca7eb514e21aa2b9d6580a6daeffcd2217925794e608eb5d30b12";

    [Fact]
    public void Sid_sequence_matches_pinned_snapshot()
    {
        var joined = string.Join("\n", ZoneContentSidCatalog.All().Select(e => e.Sid));
        var bytes = Encoding.UTF8.GetBytes(joined);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        Assert.True(ExpectedSidSequenceSha256 == hex,
            $"Catalog SID sequence hash drift. " +
            $"Expected '{ExpectedSidSequenceSha256}' but got '{hex}'. " +
            "If the catalog change is intentional, update " +
            $"{nameof(ExpectedSidSequenceSha256)} with the new value.");
    }
}
