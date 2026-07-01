#if WINDOWS
using System;
using System.Linq;
using System.Threading.Tasks;
using TimeBarX.Core;
using Windows.Services.Store;

namespace TimeBarX.App.Store;

/// <summary>
/// Microsoft Store entitlement source. Queries <see cref="StoreContext"/> for
/// the user's durable add-on purchases and exposes a single bool the rest of
/// the app reads.
///
/// Notes:
///   - <see cref="IsPro"/> is cached. Callers should treat the field as
///     advisory until <see cref="RefreshAsync"/> has completed at least once.
///     We default to <c>false</c> (fail closed) until the first refresh, so
///     transient launch states don't grant Pro to a non-purchaser.
///   - We refresh on construction (fire-and-forget), and the host should call
///     <see cref="RefreshAsync"/> on app focus / Settings open. Phase 2 only
///     wires construction-time refresh; later phases add the cadence.
///   - The Store ID for the "TimeBarX Pro" durable add-on is configured via
///     <c>TIMEBARX_PRO_STORE_ID</c> (env var) or the <see cref="ProStoreId"/>
///     constant. The placeholder ships with the code; Partner Center setup
///     swaps in the real ID. Until then, <see cref="IsPro"/> stays false.
/// </summary>
public sealed class StoreEntitlements : IEntitlements
{
    /// <summary>
    /// Partner Center Store ID for the "TimeBarX Pro" durable add-on
    /// (product EduardoTapia.TimeBarX / parent app 9P7B5MKF79DW). Queried by
    /// GetUserCollectionAsync on launch and after every purchase attempt to
    /// determine whether the user owns Pro.
    /// </summary>
    public const string ProStoreId = "9P80PM9PK9ND";

    private readonly StoreContext _context;
    private readonly string _addonStoreId;
    private bool _isPro;

    public StoreEntitlements()
    {
        _context = StoreContext.GetDefault();
        _addonStoreId = Environment.GetEnvironmentVariable("TIMEBARX_PRO_STORE_ID") ?? ProStoreId;
        // Fire-and-forget: callers see IsPro=false until the first refresh
        // completes. Errors are swallowed — Store unavailable means free tier.
        _ = RefreshAsync();
    }

    public bool IsPro => _isPro;

    public event Action? Changed;

    /// <summary>
    /// Re-query the Store for the Pro add-on. Safe to call repeatedly; only
    /// fires <see cref="Changed"/> if the cached value actually transitions.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (LooksLikePlaceholder(_addonStoreId))
        {
            // No real Store ID yet — never grant Pro from a placeholder.
            SetIsPro(false);
            return;
        }

        try
        {
            var result = await _context
                .GetUserCollectionAsync(new[] { "Durable" })
                .AsTask()
                .ConfigureAwait(false);

            var owned = result?.Products?.Values
                .Any(p => string.Equals(p.StoreId, _addonStoreId, StringComparison.OrdinalIgnoreCase)) == true;

            SetIsPro(owned);
        }
        catch
        {
            // Store unavailable / network error / package not signed by Store.
            // Fail closed: keep previous value rather than flipping to free,
            // so a transient outage doesn't lose Pro for a purchaser mid-session.
        }
    }

    /// <summary>
    /// Open the Store purchase flow for the Pro add-on. On success, refreshes
    /// the cached entitlement (which fires <see cref="Changed"/> if it flipped).
    /// Returns the raw <see cref="StorePurchaseStatus"/> so the caller can
    /// distinguish "already owned" from "succeeded" for the UX toast.
    /// </summary>
    public async Task<StorePurchaseStatus> BuyAsync()
    {
        if (LooksLikePlaceholder(_addonStoreId)) return StorePurchaseStatus.NotPurchased;
        try
        {
            var result = await _context.RequestPurchaseAsync(_addonStoreId).AsTask().ConfigureAwait(false);
            if (result.Status is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
            {
                await RefreshAsync().ConfigureAwait(false);
            }
            return result.Status;
        }
        catch
        {
            return StorePurchaseStatus.NetworkError;
        }
    }

    private void SetIsPro(bool value)
    {
        if (_isPro == value) return;
        _isPro = value;
        // RefreshAsync/BuyAsync await with ConfigureAwait(false), so this can run
        // on a thread-pool thread. Changed subscribers re-render Avalonia UI
        // (overlay layout, Settings chips, tray menu), which must touch the UI
        // thread — marshal the notification so handlers never run off-thread.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Changed?.Invoke());
    }

    private static bool LooksLikePlaceholder(string id)
        => string.IsNullOrWhiteSpace(id) || id.StartsWith("PLACEHOLDER_", StringComparison.Ordinal);
}
#endif
