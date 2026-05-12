using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneContentEmitWarnings
    {
        public static IReadOnlyList<EmitWarning> Inspect(ZoneContentItem item, string? zoneName)
        {
            var warnings = new List<EmitWarning>();

            if (item.BiomeFilter.Count > 0)
                warnings.Add(new EmitWarning(
                    EmitWarning.Codes.BiomeFilterIgnored,
                    "BiomeFilter has no schema slot and will be ignored.",
                    zoneName, item.Sid));

            if (item.FactionAffinity.Count > 0)
                warnings.Add(new EmitWarning(
                    EmitWarning.Codes.FactionAffinityIgnored,
                    "FactionAffinity has no schema slot and will be ignored.",
                    zoneName, item.Sid));

            if (item.Pool != ZoneContentPool.Mandatory)
                warnings.Add(new EmitWarning(
                    EmitWarning.Codes.PoolNonMandatoryDropped,
                    $"Pool '{item.Pool}' is not emittable in v1; item will be skipped.",
                    zoneName, item.Sid));

            if (item.MinCount != item.MaxCount)
                warnings.Add(new EmitWarning(
                    EmitWarning.Codes.MinCountRangeNarrowedToMax,
                    $"Count range {item.MinCount}-{item.MaxCount} narrowed to {item.MaxCount}.",
                    zoneName, item.Sid));

            return warnings;
        }
    }
}
