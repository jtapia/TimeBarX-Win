using System.IO;
using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class SessionHistoryStoreTests
{
    [Fact]
    public void Appended_records_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tbx-history-{Guid.NewGuid():N}.jsonl");
        try
        {
            var store = new SessionHistoryStore(path);
            var a = new SessionRecord(
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 9, 25, 0, TimeSpan.Zero),
                TimeSpan.FromMinutes(25),
                "25m",
                "morning deep work");
            var b = new SessionRecord(
                new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 10, 45, 0, TimeSpan.Zero),
                TimeSpan.FromMinutes(45),
                "45m",
                null);
            store.Append(a);
            store.Append(b);

            var read = store.Read();
            Assert.Equal(2, read.Count);
            Assert.Equal(a, read[0]);
            Assert.Equal(b, read[1]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Skips_malformed_trailing_line()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tbx-history-bad-{Guid.NewGuid():N}.jsonl");
        try
        {
            var store = new SessionHistoryStore(path);
            store.Append(new SessionRecord(
                new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 9, 25, 0, TimeSpan.Zero),
                TimeSpan.FromMinutes(25),
                "25m"));
            // Simulate a partial-append power loss: last line is truncated.
            File.AppendAllText(path, "{\"StartedAt\":\"2026-06-01T10:");

            var read = store.Read();
            Assert.Single(read);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Missing_file_returns_empty_list()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tbx-history-missing-{Guid.NewGuid():N}.jsonl");
        var store = new SessionHistoryStore(path);
        var read = store.Read();
        Assert.Empty(read);
    }
}
