using System.Text.Json;

namespace TimeBarX.Core;

/// <summary>
/// Writes JSON to disk atomically: serialize to a unique temp file in the same
/// directory, flush it to stable storage, then replace the target with a single
/// rename. A unique temp name keeps two concurrent writers from truncating each
/// other's temp file, and the flush-before-rename means a crash or power loss
/// leaves either the old file or the complete new one, never a truncated target.
/// </summary>
internal static class AtomicFile
{
    public static void WriteJson<T>(string path, T value, JsonSerializerOptions options)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Unique per write so overlapping saves don't share a temp file.
        var tmp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, options);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>
    /// Deletes temp files orphaned by a hard crash between CreateNew and Move for
    /// the given target (siblings matching <c>&lt;path&gt;.*.tmp</c>). A clean write
    /// removes its own temp, so any remaining one is from a crashed process. Best
    /// effort and safe to call at startup; skips temp files still held open by a
    /// concurrent live writer (their delete throws and is swallowed).
    /// </summary>
    public static void SweepOrphanedTemps(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            var prefix = Path.GetFileName(path);
            foreach (var stale in Directory.EnumerateFiles(dir, $"{prefix}.*.tmp"))
                TryDelete(stale);
        }
        catch
        {
            // best-effort sweep
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
