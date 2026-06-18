using System.Globalization;
using System.Text.RegularExpressions;

namespace TimeBarX.Core;

public sealed record ParsedDuration(TimeSpan Duration, string? Label, string Preset);

public static class DurationParser
{
    // "1:30" / "0:45" / "1:23:45" — colon-separated time.
    private static readonly Regex ColonForm = new(
        @"^(?<a>\d+):(?<b>\d{1,2})(?::(?<c>\d{1,2}))?\b",
        RegexOptions.Compiled);

    // "2h 15m 30s" — one or more unit groups. Each group's unit is h/m/s (longer forms expand them).
    // The unit must terminate at end-of-string, whitespace, or another digit (next unit).
    private static readonly Regex UnitForm = new(
        @"^(?:(?<n>\d+)\s*(?<u>hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)(?=\s|\d|$|[^A-Za-z0-9])\s*)+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UnitToken = new(
        @"(?<n>\d+)\s*(?<u>hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)(?=\s|\d|$|[^A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryParse(string input, out ParsedDuration result)
    {
        result = default!;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var trimmed = input.Trim();

        if (TryParseColon(trimmed, out result)) return true;
        if (TryParseUnits(trimmed, out result)) return true;
        if (TryParsePhrase(trimmed, out result)) return true;

        return false;
    }

    // Phrase forms: "half hour" / "quarter hour" / "an hour" / "a minute" — with optional trailing label.
    private static readonly Regex PhraseForm = new(
        @"^(?<q>half|quarter|an?)\s+(?<u>hour|minute)s?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryParsePhrase(string input, out ParsedDuration result)
    {
        result = default!;
        var m = PhraseForm.Match(input);
        if (!m.Success) return false;

        var q = m.Groups["q"].Value.ToLowerInvariant();
        var u = m.Groups["u"].Value.ToLowerInvariant();
        var unit = u[0] == 'h' ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(1);
        TimeSpan duration = q switch
        {
            "half" => unit == TimeSpan.FromHours(1) ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(30),
            "quarter" => unit == TimeSpan.FromHours(1) ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(15),
            _ => unit, // "a" / "an"
        };

        if (duration <= TimeSpan.Zero) return false;

        var preset = duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h"
            : $"{(int)duration.TotalMinutes}m";

        var label = ExtractTrailingLabel(input, m.Length);
        result = new ParsedDuration(duration, label, preset);
        return true;
    }

    private static bool TryParseColon(string input, out ParsedDuration result)
    {
        result = default!;
        var m = ColonForm.Match(input);
        if (!m.Success) return false;

        var a = int.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture);
        var b = int.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
        int? c = m.Groups["c"].Success
            ? int.Parse(m.Groups["c"].Value, CultureInfo.InvariantCulture)
            : null;

        TimeSpan duration;
        string preset;
        if (c is null)
        {
            // a:b → minutes:seconds when a < 60 and there's no explicit hour context.
            // Heuristic: treat "1:30" as 1h30m (matches PLAN.md example).
            duration = new TimeSpan(a, b, 0);
            preset = $"{a}:{b:D2}";
        }
        else
        {
            duration = new TimeSpan(a, b, c.Value);
            preset = $"{a}:{b:D2}:{c.Value:D2}";
        }

        if (duration <= TimeSpan.Zero) return false;

        var label = ExtractTrailingLabel(input, m.Length);
        result = new ParsedDuration(duration, label, preset);
        return true;
    }

    private static bool TryParseUnits(string input, out ParsedDuration result)
    {
        result = default!;
        var head = UnitForm.Match(input);
        if (!head.Success || head.Length == 0) return false;

        var total = TimeSpan.Zero;
        var presetParts = new List<string>();

        foreach (Match token in UnitToken.Matches(head.Value))
        {
            var n = int.Parse(token.Groups["n"].Value, CultureInfo.InvariantCulture);
            var u = token.Groups["u"].Value.ToLowerInvariant();

            switch (u[0])
            {
                case 'h':
                    total += TimeSpan.FromHours(n);
                    presetParts.Add($"{n}h");
                    break;
                case 'm':
                    total += TimeSpan.FromMinutes(n);
                    presetParts.Add($"{n}m");
                    break;
                case 's':
                    total += TimeSpan.FromSeconds(n);
                    presetParts.Add($"{n}s");
                    break;
            }
        }

        if (total <= TimeSpan.Zero) return false;

        var label = ExtractTrailingLabel(input, head.Length);
        result = new ParsedDuration(total, label, string.Join(' ', presetParts));
        return true;
    }

    private static string? ExtractTrailingLabel(string input, int consumed)
    {
        if (consumed >= input.Length) return null;
        var tail = input[consumed..].Trim();
        return tail.Length == 0 ? null : tail;
    }
}
