using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using SchemaContentItem = OldenEra.Generator.Models.Unfrozen.ContentItem;
using SchemaContentRule = OldenEra.Generator.Models.Unfrozen.ContentPlacementRule;
using SchemaMandatoryGroup = OldenEra.Generator.Models.Unfrozen.MandatoryContentGroup;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneContentEmitter
    {
        public sealed record EmitResult(IReadOnlyList<EmitWarning> Warnings);

        public static EmitResult ApplyToMandatoryGroup(
            SchemaMandatoryGroup group,
            IReadOnlyList<ZoneContentItem> items,
            string zoneName,
            IReadOnlySet<string> referencedNames)
        {
            var warnings = new List<EmitWarning>();
            group.Content ??= new List<SchemaContentItem>();

            var occurrenceBySid = new Dictionary<string, int>(System.StringComparer.Ordinal);

            foreach (var item in items)
            {
                var itemWarnings = ZoneContentEmitWarnings.Inspect(item, zoneName);
                warnings.AddRange(itemWarnings);

                if (itemWarnings.Any(w => w.Code == EmitWarning.Codes.PoolNonMandatoryDropped))
                    continue;

                int occurrence = occurrenceBySid.TryGetValue(item.Sid, out var n) ? n : 0;
                occurrenceBySid[item.Sid] = occurrence + 1;

                string? name = ResolveName(item, zoneName, occurrence, referencedNames);

                for (int copy = 0; copy < item.MaxCount; copy++)
                {
                    var row = new SchemaContentItem
                    {
                        Name = name,
                        IsGuarded = item.IsGuarded ? true : (bool?)null,
                    };
                    if (item.IsGroup) row.IncludeLists = new List<string> { item.Sid };
                    else row.Sid = item.Sid;

                    var rules = BuildPlacementRules(item);
                    if (rules.Count > 0) row.Rules = rules;

                    group.Content.Add(row);
                }
            }

            return new EmitResult(warnings);
        }

        private static string? ResolveName(
            ZoneContentItem item, string zoneName, int occurrence,
            IReadOnlySet<string> referencedNames)
        {
            if (!string.IsNullOrEmpty(item.Handle)) return item.Handle;
            var auto = $"name_user_{zoneName}_{item.Sid}_{occurrence}";
            return referencedNames.Contains(auto) ? auto : null;
        }

        private static List<SchemaContentRule> BuildPlacementRules(ZoneContentItem item)
        {
            var rules = new List<SchemaContentRule>();

            if (item.NearCastle)
                rules.Add(new SchemaContentRule
                {
                    Type = "MainObject",
                    Args = new List<string> { "0" },
                    TargetMin = 0.05,
                    TargetMax = 0.25,
                    Weight = 1
                });

            if (item.RoadDistance is { } rd)
            {
                var (min, max) = rd switch
                {
                    RoadDistance.Close => (0.10, 0.20),
                    RoadDistance.Mid   => (0.30, 0.50),
                    RoadDistance.Far   => (0.60, 0.85),
                    _ => (0.0, 0.0),
                };
                rules.Add(new SchemaContentRule
                {
                    Type = "Road",
                    Args = new List<string>(),
                    TargetMin = min,
                    TargetMax = max,
                    Weight = 1
                });
            }

            return rules;
        }
    }
}
