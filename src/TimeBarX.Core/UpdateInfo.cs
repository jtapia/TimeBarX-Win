namespace TimeBarX.Core;

public sealed record UpdateInfo(string LatestVersion, string DownloadUrl)
{
    /// <summary>
    /// Returns true when <paramref name="latest"/> is newer than <paramref name="current"/>.
    /// Both must be dotted numeric versions; non-numeric segments are ignored.
    /// </summary>
    public static bool IsNewer(string latest, string current)
    {
        var a = Parse(latest);
        var b = Parse(current);
        var len = Math.Max(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            var ai = i < a.Length ? a[i] : 0;
            var bi = i < b.Length ? b[i] : 0;
            if (ai > bi) return true;
            if (ai < bi) return false;
        }
        return false;
    }

    private static int[] Parse(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return Array.Empty<int>();
        // Tolerate a leading "v" (GitHub tag style) and per-segment suffixes
        // ("1.2.0-beta" → 1.2.0) by reading the leading digits of each segment.
        var trimmed = v.Trim();
        if (trimmed.Length > 0 && (trimmed[0] == 'v' || trimmed[0] == 'V')) trimmed = trimmed[1..];
        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            result[i] = LeadingInt(parts[i]);
        }
        return result;
    }

    private static int LeadingInt(string segment)
    {
        var end = 0;
        while (end < segment.Length && char.IsAsciiDigit(segment[end])) end++;
        return end > 0 && int.TryParse(segment.AsSpan(0, end), out var n) ? n : 0;
    }
}
