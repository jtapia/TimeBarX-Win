namespace TimeBarX.Core;

/// <summary>
/// Combines any number of entitlement sources: Pro if <em>any</em> source
/// reports Pro. Used to merge the Store IAP, the direct license-key channel, and
/// the time-limited trial so Pro granted through one channel is honored
/// everywhere, and a live transition in any source re-renders the app.
///
/// <para>
/// Unlike a fixed binary combinator, this scales to N sources without nesting
/// and is <see cref="IDisposable"/>: it unsubscribes from every source on
/// <see cref="Dispose"/> so a rebuilt composite doesn't leave the long-lived
/// sources (Store/Mock singletons) firing into a dead handler.
/// </para>
/// </summary>
public sealed class CompositeEntitlements : IEntitlements, IDisposable
{
    private readonly IEntitlements[] _sources;
    private bool _disposed;

    public CompositeEntitlements(params IEntitlements[] sources)
    {
        _sources = sources ?? Array.Empty<IEntitlements>();
        foreach (var source in _sources)
        {
            source.Changed += Forward;
        }
    }

    /// <summary>True if any composed source currently reports Pro.</summary>
    public bool IsPro
    {
        get
        {
            foreach (var source in _sources)
            {
                if (source.IsPro) return true;
            }
            return false;
        }
    }

    public event Action? Changed;

    private void Forward() => Changed?.Invoke();

    /// <summary>Unsubscribes from every source. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var source in _sources)
        {
            source.Changed -= Forward;
        }
    }
}
