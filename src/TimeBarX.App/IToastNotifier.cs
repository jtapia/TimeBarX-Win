namespace TimeBarX.App;

/// <summary>
/// Data for a timer-completion toast. The URIs are timebarx:// commands the
/// toast buttons carry; clicking a button re-activates the app through the
/// existing protocol handler (App.HandleUri), so the same path works whether the
/// app is running or was closed.
/// </summary>
/// <param name="Title">Toast heading, e.g. "Timer complete".</param>
/// <param name="Body">Secondary line, e.g. the timer's label, or empty.</param>
/// <param name="RestartUri">timebarx:// URI that restarts the same duration/label.</param>
/// <param name="ExtendUri">
/// timebarx:// URI that starts a fresh +5 minute timer, or <c>null</c> to omit
/// the "+5 min" button entirely — used during an active Pomodoro cycle, where
/// starting a bare 5-minute timer would silently abandon the phase chain.
/// </param>
public readonly record struct ToastCompletionInfo(
    string Title,
    string Body,
    string RestartUri,
    string? ExtendUri);

/// <summary>
/// Raises native Windows completion toasts. Additive to the existing sound and
/// overlay flash — never a replacement. Must degrade to a no-op (never throw)
/// on non-Windows, when notifications are disabled/suppressed by the OS, or when
/// the toast platform is otherwise unavailable.
/// </summary>
public interface IToastNotifier
{
    void ShowCompletion(ToastCompletionInfo info);
}

/// <summary>No-op notifier for non-Windows builds and when toasts are disabled.</summary>
public sealed class NullToastNotifier : IToastNotifier
{
    public void ShowCompletion(ToastCompletionInfo info)
    {
    }
}
