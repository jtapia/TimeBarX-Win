using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeBarX.Core;

/// <summary>
/// Append-only JSONL log of completed sessions. Each line is one
/// <see cref="SessionRecord"/> so the file stays cheap to append to and
/// tolerant of truncation — a partial trailing line is skipped on read
/// rather than corrupting the whole log.
/// </summary>
public sealed class SessionHistoryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly object _lock = new();

    public SessionHistoryStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public string Path => _path;

    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(dir, "TimeBarX", "history.jsonl");
    }

    public void Append(SessionRecord record)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var line = JsonSerializer.Serialize(record, Options) + "\n";
            lock (_lock)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // History logging is best-effort — a full disk or read-only mount
            // must never crash the completion pipeline.
        }
    }

    /// <summary>
    /// Reads the log, skipping malformed lines. Returns records in insertion
    /// order (oldest first).
    /// </summary>
    public IReadOnlyList<SessionRecord> Read()
    {
        var result = new List<SessionRecord>();
        try
        {
            if (!File.Exists(_path)) return result;
            foreach (var line in File.ReadAllLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<SessionRecord>(line, Options);
                    if (record is not null) result.Add(record);
                }
                catch
                {
                    // Skip a partial trailing line (e.g. mid-append power loss).
                }
            }
        }
        catch
        {
            // best-effort read
        }
        return result;
    }
}
