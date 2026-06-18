namespace TimeBarX.Core;

public enum UriCommandKind
{
    Start,
    Pause,
    Resume,
    Stop,
}

public sealed record UriCommand(
    UriCommandKind Kind,
    TimeSpan? Duration = null,
    string? Preset = null,
    string? Label = null)
{
    public const string Scheme = "timebarx";

    public static bool TryParse(string input, out UriCommand command)
    {
        command = default!;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)) return false;

        var action = uri.Host.ToLowerInvariant();
        var query = ParseQuery(uri.Query);

        switch (action)
        {
            case "start":
                if (!query.TryGetValue("duration", out var rawDuration) || string.IsNullOrWhiteSpace(rawDuration))
                    return false;
                if (!DurationParser.TryParse(rawDuration, out var parsed)) return false;
                query.TryGetValue("label", out var labelOverride);
                var label = !string.IsNullOrWhiteSpace(labelOverride) ? labelOverride : parsed.Label;
                command = new UriCommand(UriCommandKind.Start, parsed.Duration, parsed.Preset, label);
                return true;

            case "pause":
                command = new UriCommand(UriCommandKind.Pause);
                return true;
            case "resume":
                command = new UriCommand(UriCommandKind.Resume);
                return true;
            case "stop":
                command = new UriCommand(UriCommandKind.Stop);
                return true;
            default:
                return false;
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }
            var k = Uri.UnescapeDataString(pair[..eq]);
            var v = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[k] = v;
        }
        return result;
    }
}
