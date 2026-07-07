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
///   - We refresh on construction (fire-and-forget). <see cref="BuyAsync"/>
///     re-refreshes on its own after a successful purchase, and the "Restore
///     purchases" button in <c>UpgradeProDialog</c> calls
///     <see cref="RefreshAsync"/> directly. Callers can invoke it themselves
///     if they need a fresh signal outside those paths.
///   - The Store ID for the "TimeBarX Pro" durable add-on is configured via
///     <c>TIMEBARX_PRO_STORE_ID</c> (env var) or the <see cref="ProStoreId"/>
///     constant. The env var lets a signed build be pointed at a sandbox
///     add-on without a rebuild.
/// </summary>
public sealed class StoreEntitlements : IEntitlements
{
    /// <summary>
    /// Partner Center Store ID for the "TimeBarX Pro" durable add-on
    /// (parent app: EduardoTapia.TimeBarX). Queried by GetUserCollectionAsync
    /// on launch and after every purchase attempt to determine whether the
    /// user owns Pro.
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

    /// <summary>Human-readable detail from the last failed purchase attempt, if any.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Open the Store purchase flow for the Pro add-on. On success, refreshes
    /// the cached entitlement (which fires <see cref="Changed"/> if it flipped).
    /// Returns the raw <see cref="StorePurchaseStatus"/> so the caller can
    /// distinguish "already owned" from "succeeded" for the UX toast.
    /// </summary>
    /// <param name="ownerHwnd">Top-level window handle the Store purchase dialog
    /// is parented to; required on desktop or the call fails with 0x80070578.</param>
    public async Task<StorePurchaseStatus> BuyAsync(IntPtr ownerHwnd)
    {
        LastError = null;
        if (LooksLikePlaceholder(_addonStoreId)) return StorePurchaseStatus.NotPurchased;
        try
        {
            // Desktop apps must associate StoreContext with a top-level HWND
            // before RequestPurchaseAsync — otherwise the runtime can't parent
            // the purchase dialog and throws "Invalid window handle" (0x80070578).
            // GetUserCollectionAsync doesn't need this, which is why refresh
            // worked but purchase did not.
            WinRT.Interop.InitializeWithWindow.Initialize(_context, ownerHwnd);
            var result = await _context.RequestPurchaseAsync(_addonStoreId).AsTask().ConfigureAwait(false);
            if (result.Status is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
            {
                await RefreshAsync().ConfigureAwait(false);
            }
            if (result.ExtendedError is { } err)
                LastError = $"HRESULT=0x{err.HResult:X8} {err.Message}";
            return result.Status;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message} (HRESULT=0x{ex.HResult:X8})";
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
