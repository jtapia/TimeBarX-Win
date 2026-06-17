namespace TimeBarX.Core;

public interface ITimerStore
{
    TimerSnapshot? Load();
    void Save(TimerSnapshot snapshot);
    void Clear();
}
