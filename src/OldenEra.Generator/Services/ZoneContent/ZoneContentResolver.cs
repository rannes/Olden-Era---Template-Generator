using System;
using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    public static class ZoneContentResolver
    {
        public static ZoneContentList Resolve(
            NeutralZoneContent cfg,
            NeutralZoneTier tier,
            string zoneLetter)
        {
            var byKey = new Dictionary<string, ZoneContentItem>(StringComparer.Ordinal);
            var order = new List<string>();

            void Apply(IEnumerable<ZoneContentItem>? items)
            {
                if (items == null) return;
                foreach (var item in items)
                {
                    if (!byKey.ContainsKey(item.Sid))
                    {
                        order.Add(item.Sid);
                    }
                    byKey[item.Sid] = item;
                }
            }

            Apply(cfg.Global.Items);
            if (cfg.ByTier.TryGetValue(tier, out var tierList))
                Apply(tierList.Items);
            if (cfg.ByZoneLetter.TryGetValue(zoneLetter, out var letterList))
                Apply(letterList.Items);

            var result = new ZoneContentList();
            foreach (var sid in order)
                result.Items.Add(byKey[sid]);
            return result;
        }
    }
}
