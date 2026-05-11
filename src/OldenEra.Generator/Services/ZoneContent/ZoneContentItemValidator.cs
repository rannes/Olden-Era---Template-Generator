using System.Collections.Generic;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent
{
    /// <summary>
    /// Validates a single <see cref="ZoneContentItem"/> for self-consistency,
    /// returning human-readable issues that hosts can render.
    /// </summary>
    public static class ZoneContentItemValidator
    {
        /// <summary>
        /// Checks the item against the following rules and returns a list of
        /// issues (empty when the item is healthy):
        /// <list type="bullet">
        ///   <item><description><c>Sid</c> must be non-empty.</description></item>
        ///   <item><description><c>MinCount</c> must be &gt;= 0.</description></item>
        ///   <item><description><c>MaxCount</c> must be &gt;= <c>MinCount</c>.</description></item>
        ///   <item><description><c>NearCastle</c> is incompatible with <c>RoadDistance=Far</c>.</description></item>
        ///   <item><description><c>RoadDistance</c>, when set to a non-empty value, must be one of the
        ///     canonical PascalCase strings <c>Close</c>, <c>Mid</c>, or <c>Far</c> (case-sensitive).
        ///     <c>null</c> and empty strings are treated as "unset" and skip this rule.</description></item>
        /// </list>
        /// </summary>
        /// <param name="item">The zone content item to validate.</param>
        /// <returns>A read-only list of human-readable issue messages; empty when valid.</returns>
        public static IReadOnlyList<string> Validate(ZoneContentItem item)
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(item.Sid))
                issues.Add("Sid must be non-empty.");

            if (item.MinCount < 0)
                issues.Add("MinCount must be >= 0.");

            if (item.MaxCount < item.MinCount)
                issues.Add($"MaxCount ({item.MaxCount}) must be >= MinCount ({item.MinCount}).");

            if (item.NearCastle && item.RoadDistance == "Far")
                issues.Add("NearCastle is incompatible with RoadDistance=Far.");

            if (!string.IsNullOrEmpty(item.RoadDistance)
                && item.RoadDistance != "Close"
                && item.RoadDistance != "Mid"
                && item.RoadDistance != "Far")
            {
                issues.Add($"RoadDistance '{item.RoadDistance}' must be one of: Close, Mid, Far.");
            }

            return issues;
        }
    }
}
