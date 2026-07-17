using System.Globalization;
using System.Linq;
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
        // Compound phrase must run before the unit form: "2 hours and 30 minutes"
        // would otherwise be consumed as "2 hours" alone with " and 30 minutes"
        // captured as a label.
        if (TryParseCompoundPhrase(trimmed, out result)) return true;
        if (TryParseUnits(trimmed, out result)) return true;
        if (TryParsePhrase(trimmed, out result)) return true;

        return false;
    }

    // Compound phrases: "N hour(s) and M min(utes)", "an hour and 15",
    // "an hour and a half", "2 hours and a quarter". Each side may be a
    // number-with-unit, a bare number (interpreted in the smaller sibling
    // unit), or a fractional word ("half"/"quarter") of the larger unit.
    // Whichever side gives us a positive TimeSpan wins.
    private static readonly Regex CompoundPhraseForm = new(
        @"^(?<h>\d+|an?)\s+(?<hu>hours?|hrs?)\s+and\s+(?<tail>a\s+half|a\s+quarter|half|quarter|\d+\s*(?:minutes?|mins?|m)?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryParseCompoundPhrase(string input, out ParsedDuration result)
    {
        result = default!;
        var m = CompoundPhraseForm.Match(input);
        if (!m.Success) return false;

        var head = m.Groups["h"].Value.ToLowerInvariant();
        int hours;
        if (head is "a" or "an") hours = 1;
        else if (!TryParseComponent(head, MaxHours, out hours) || hours <= 0) return false;

        var tail = m.Groups["tail"].Value.ToLowerInvariant().Trim();
        TimeSpan extra;
        if (tail is "half" or "a half") extra = TimeSpan.FromMinutes(30);
        else if (tail is "quarter" or "a quarter") extra = TimeSpan.FromMinutes(15);
        else
        {
            // "15" or "15 minutes" — bare digits default to minutes.
            var digits = new string(tail.TakeWhile(char.IsDigit).ToArray());
            if (!TryParseComponent(digits, MaxHours * 60, out var minutes) || minutes <= 0) return false;
            if (minutes >= 60) return false; // "an hour and 90" is nonsense; use "2:30" instead
            extra = TimeSpan.FromMinutes(minutes);
        }

        var duration = TimeSpan.FromHours(hours) + extra;
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(MaxHours)) return false;

        var preset = PresetFor(duration);
        var label = ExtractTrailingLabel(input, m.Length);
        result = new ParsedDuration(duration, label, preset);
        return true;
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
        var isHour = m.Groups["u"].Value[0] is 'h' or 'H';
        TimeSpan duration = q switch
        {
            "half" => isHour ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(30),
            "quarter" => isHour ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(15),
            _ => isHour ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(1), // "a" / "an"
        };

        if (duration <= TimeSpan.Zero) return false;

        var preset = PresetFor(duration);

        var label = ExtractTrailingLabel(input, m.Length);
        result = new ParsedDuration(duration, label, preset);
        return true;
    }

    // Largest hour component we accept. Guards against int/TimeSpan overflow on
    // untrusted input (e.g. a "timebarx://start?duration=99999999999h" link) and
    // keeps durations in a sane range for a timer.
    private const int MaxHours = 999;

    private static bool TryParseColon(string input, out ParsedDuration result)
    {
        result = default!;
        var m = ColonForm.Match(input);
        if (!m.Success) return false;

        if (!TryParseComponent(m.Groups["a"].Value, MaxHours, out var a)) return false;
        if (!TryParseComponent(m.Groups["b"].Value, 59, out var b)) return false;
        int c = 0;
        if (m.Groups["c"].Success && !TryParseComponent(m.Groups["c"].Value, 59, out c)) return false;

        TimeSpan duration;
        string preset;
        if (!m.Groups["c"].Success)
        {
            // Two-part colon form is always hours:minutes (never minutes:seconds),
            // so "1:30" is 1h30m and "90:00" is 90h — matches the PLAN.md example
            // and the preset round-trip. Use "1:30:00" for a three-part h:m:s form.
            duration = new TimeSpan(a, b, 0);
            preset = $"{a}:{b:D2}";
        }
        else
        {
            duration = new TimeSpan(a, b, c);
            preset = $"{a}:{b:D2}:{c:D2}";
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
            var u = token.Groups["u"].Value.ToLowerInvariant();

            // Bound each component to guard against int/TimeSpan overflow on
            // untrusted input; the hour cap keeps the summed total sane too.
            var limit = u[0] switch
            {
                'h' => MaxHours,
                'm' => MaxHours * 60,
                _ => MaxHours * 3600,
            };
            if (!TryParseComponent(token.Groups["n"].Value, limit, out var n)) return false;

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

        if (total <= TimeSpan.Zero || total > TimeSpan.FromHours(MaxHours)) return false;

        var label = ExtractTrailingLabel(input, head.Length);
        result = new ParsedDuration(total, label, string.Join(' ', presetParts));
        return true;
    }

    // Renders a canonical preset string that round-trips back through TryParse.
    // Sub-minute durations must use seconds so we never emit a dead "0m".
    private static string PresetFor(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var mins = duration.Minutes;
            return mins == 0 ? $"{hours}h" : $"{hours}h {mins}m";
        }
        if (duration.TotalMinutes >= 1) return $"{(int)duration.TotalMinutes}m";
        return $"{(int)duration.TotalSeconds}s";
    }

    // Parses a numeric component, rejecting values that don't fit in an int or
    // exceed the given inclusive upper bound. The regex only matches \d+, so
    // failure here means the number was too large rather than malformed.
    private static bool TryParseComponent(string value, int max, out int result)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result)
            && result <= max;
    }

    private static string? ExtractTrailingLabel(string input, int consumed)
    {
        if (consumed >= input.Length) return null;
        var tail = input[consumed..].Trim();
        return tail.Length == 0 ? null : tail;
    }
}
