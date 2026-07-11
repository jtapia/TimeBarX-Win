using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class JsonTimerStoreTests : IDisposable
{
    private readonly string _path;

    public JsonTimerStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"timebarx-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Load_returns_null_when_missing()
    {
        var store = new JsonTimerStore(_path);
        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_then_load_round_trips_running_snapshot()
    {
        var store = new JsonTimerStore(_path);
        var end = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new TimerSnapshot(
            TimerState.Running, end, TimeSpan.FromMinutes(25), TimeSpan.Zero, "25m");

        store.Save(snapshot);
        var loaded = store.Load();

        Assert.Equal(snapshot, loaded);
    }

    [Fact]
    public void Save_then_load_round_trips_paused_snapshot()
    {
        var store = new JsonTimerStore(_path);
        var snapshot = new TimerSnapshot(
            TimerState.Paused, null, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(7), "25m");

        store.Save(snapshot);
        var loaded = store.Load();

        Assert.Equal(snapshot, loaded);
    }

    [Fact]
    public void Clear_removes_file()
    {
        var store = new JsonTimerStore(_path);
        store.Save(new TimerSnapshot(TimerState.Idle, null, TimeSpan.Zero, TimeSpan.Zero, null));

        store.Clear();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_returns_null_on_corrupted_file()
    {
        File.WriteAllText(_path, "{ not valid json");
        var store = new JsonTimerStore(_path);

        Assert.Null(store.Load());
    }

    [Fact]
    public void State_is_persisted_as_a_string_not_an_integer()
    {
        var store = new JsonTimerStore(_path);
        store.Save(new TimerSnapshot(
            TimerState.Running, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), TimeSpan.Zero, "5m"));

        var json = File.ReadAllText(_path);
        Assert.Contains("\"Running\"", json);
        Assert.DoesNotContain("\"State\":1", json);
    }

    [Fact]
    public void Concurrent_saves_never_leave_a_corrupt_target()
    {
        var store = new JsonTimerStore(_path);
        var snapshot = new TimerSnapshot(
            TimerState.Running, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(25), TimeSpan.Zero, "25m");

        Parallel.For(0, 50, _ => store.Save(snapshot));

        // The target must always be a fully-written, parseable file.
        Assert.NotNull(store.Load());
        // No leftover temp files in the directory.
        var dir = Path.GetDirectoryName(_path)!;
        Assert.Empty(Directory.GetFiles(dir, Path.GetFileName(_path) + ".*.tmp"));
    }
}
