using System;
using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    /// <summary>
    /// Merges the layered <see cref="NeutralZoneContent"/> configuration into a
    /// single resolved <see cref="ZoneContentList"/> for emission.
    /// </summary>
    public static class ZoneContentResolver
    {
        /// <summary>
        /// Resolves the effective zone content list by applying layers in order:
        /// <c>Global → ByTier[tier] → ByZoneLetter[zoneLetter]</c>.
        /// </summary>
        /// <remarks>
        /// Items are appended on first sight of their <c>Sid</c>, preserving the
        /// original insertion order. Same-<c>Sid</c> entries from later layers
        /// REPLACE earlier values in place; they do not duplicate the entry and
        /// do not move its position. Returns an empty <see cref="ZoneContentList"/>
        /// if all layers are empty.
        /// </remarks>
        /// <param name="cfg">The layered neutral zone content configuration.</param>
        /// <param name="tier">The neutral zone tier whose layer should be applied.</param>
        /// <param name="zoneLetter">The zone letter whose layer should be applied.</param>
        /// <returns>The merged content list, in the order each <c>Sid</c> first appeared.</returns>
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
