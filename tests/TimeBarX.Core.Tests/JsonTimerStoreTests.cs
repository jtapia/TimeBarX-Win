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
}
