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
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            result[i] = int.TryParse(parts[i], out var n) ? n : 0;
        }
        return result;
    }
}
