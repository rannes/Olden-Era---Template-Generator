using System.Collections.Generic;
using System.Globalization;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services.ZoneContent;

/// <summary>
/// Codec for the per-row "rules" CSV used by the host UIs (T-202).
/// Mirrors the <see cref="ZoneContentRule"/> shape inside a single line so the
/// existing single-row item editor stays compact (no nested row-editor):
/// <c>Type|args1/args2|min|max|weight; Type|...</c>
/// <para>Each rule has 5 fields separated by <c>|</c>. <c>args</c> is a
/// slash-separated sub-list (empty allowed). Decimal numbers parse with
/// invariant culture so a comma-decimal locale doesn't collide with the
/// rule separator.</para>
/// <para>Malformed tokens are silently dropped — round-tripping a hand-edited
/// .oetgs file should never crash the host, even if a user fat-fingers a
/// number. <see cref="ZoneContentItem.Rules"/> remains the authoritative
/// in-memory shape; the CSV is purely UI / shareable representation.</para>
/// </summary>
public static class ZoneContentRuleCsv
{
    private const char RuleSeparator = ';';
    private const char FieldSeparator = '|';
    private const char ArgSeparator = '/';

    /// <summary>Parses a CSV string into a list of rules. Empty / whitespace
    /// input → empty list. Malformed entries are skipped, not thrown.</summary>
    public static List<ZoneContentRule> Parse(string? raw)
    {
        var result = new List<ZoneContentRule>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var part in raw.Split(RuleSeparator))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;

            var fields = trimmed.Split(FieldSeparator);
            // Type is required. Other fields default to "unset".
            if (fields.Length == 0) continue;
            var type = fields[0].Trim();
            if (type.Length == 0) continue;

            var rule = new ZoneContentRule { Type = type };
            if (fields.Length >= 2)
            {
                var argsRaw = fields[1];
                if (!string.IsNullOrEmpty(argsRaw))
                {
                    foreach (var a in argsRaw.Split(ArgSeparator))
                    {
                        var t = a.Trim();
                        if (t.Length > 0) rule.Args.Add(t);
                    }
                }
            }
            if (fields.Length >= 3) rule.TargetMin = ParseDouble(fields[2]);
            if (fields.Length >= 4) rule.TargetMax = ParseDouble(fields[3]);
            if (fields.Length >= 5) rule.Weight    = ParseDouble(fields[4]);

            result.Add(rule);
        }
        return result;
    }

    /// <summary>Renders the in-memory list as the single-line CSV form.</summary>
    public static string Join(IReadOnlyList<ZoneContentRule>? rules)
    {
        if (rules is null || rules.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < rules.Count; i++)
        {
            if (i > 0) sb.Append("; ");
            var r = rules[i];
            sb.Append(r.Type ?? "");
            sb.Append(FieldSeparator);
            sb.Append(string.Join(ArgSeparator, r.Args));
            sb.Append(FieldSeparator);
            sb.Append(FormatDouble(r.TargetMin));
            sb.Append(FieldSeparator);
            sb.Append(FormatDouble(r.TargetMax));
            sb.Append(FieldSeparator);
            sb.Append(FormatDouble(r.Weight));
        }
        return sb.ToString();
    }

    private static double? ParseDouble(string raw)
    {
        var t = raw.Trim();
        if (t.Length == 0) return null;
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : (double?)null;
    }

    private static string FormatDouble(double? v) =>
        v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : "";
}
