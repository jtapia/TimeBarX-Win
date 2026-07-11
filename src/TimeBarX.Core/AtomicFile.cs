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
