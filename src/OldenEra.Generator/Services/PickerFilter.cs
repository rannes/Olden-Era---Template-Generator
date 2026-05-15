using System;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// T-804: shared substring-match helper used by every picker (heroes, spells,
    /// units, content lists, SIDs) on both hosts. Keeps filter semantics identical
    /// across Web and WPF and gives us one place to test the matching rules.
    /// </summary>
    public static class PickerFilter
    {
        /// <summary>
        /// Returns true when <paramref name="filter"/> is empty/whitespace, or when
        /// any of the supplied haystack strings contains <paramref name="filter"/>
        /// (case-insensitive, ordinal). Null haystack entries are skipped.
        /// </summary>
        public static bool Matches(string? filter, params string?[] haystacks)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            if (haystacks is null || haystacks.Length == 0) return false;

            var needle = filter.Trim();
            foreach (var h in haystacks)
            {
                if (string.IsNullOrEmpty(h)) continue;
                if (h.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
