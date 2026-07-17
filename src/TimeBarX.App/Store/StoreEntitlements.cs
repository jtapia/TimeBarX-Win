#if WINDOWS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TimeBarX.Core;
using Windows.Services.Store;

namespace TimeBarX.App.Store;

/// <summary>Outcome of a Store entitlement refresh, so callers can tell "not owned" from "couldn't check".</summary>
public enum RefreshResult
{
    Owned,
    NotOwned,
    CheckFailed,
    /// <summary>No Store runtime at all (direct/Inno build, no MSIX identity).
    /// Distinct from CheckFailed: retrying can never succeed, so the caller
    /// should point the user at the license-key flow, not "check your connection".</summary>
    NoStoreRuntime,
}

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

    private readonly StoreContext? _context;
    private readonly string _addonStoreId;
    private readonly string _cachePath;
    private bool _isPro;

    public StoreEntitlements(string? cachePath = null)
    {
        // StoreContext requires MSIX package identity. The direct (Inno) build is
        // compiled with the windows TFM too, so GetDefault() runs there without
        // identity and can throw a COMException; guard it so the app doesn't crash
        // at startup — a null context simply means the Store channel is inert and
        // the license-key channel handles direct-channel Pro.
        try
        {
            _context = StoreContext.GetDefault();
        }
        catch
        {
            _context = null;
        }

        _addonStoreId = Environment.GetEnvironmentVariable("TIMEBARX_PRO_STORE_ID") ?? ProStoreId;
        _cachePath = cachePath ?? DefaultCachePath();

        // Seed from the last known-good result so a purchaser keeps Pro while
        // offline (or the Store service is down) instead of being locked out
        // until a refresh succeeds. The background refresh below corrects it
        // (e.g. after a refund) once the Store is reachable.
        _isPro = ReadCachedPro();

        // Fire-and-forget refresh. Errors are swallowed — an unreachable Store
        // leaves the cached value in place rather than flipping a purchaser to free.
        _ = RefreshAsync();
    }

    /// <summary>True when the Store runtime is available (MSIX identity present).</summary>
    public bool IsStoreAvailable => _context is not null;

    private static string DefaultCachePath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "TimeBarX", "entitlement.cache");
    }

    public bool IsPro => _isPro;

    public event Action? Changed;

    /// <summary>
    /// Re-query the Store for the Pro add-on. Safe to call repeatedly; only
    /// fires <see cref="Changed"/> if the cached value actually transitions.
    /// Returns whether the user owns Pro, or <see cref="RefreshResult.CheckFailed"/>
    /// when the Store couldn't be reached — so callers (e.g. Restore) don't
    /// report a network failure as "you never purchased".
    /// </summary>
    public async Task<RefreshResult> RefreshAsync()
    {
        if (_context is null)
        {
            // No Store runtime (direct build / no package identity): the cached
            // value stands and the license-key channel owns Pro here. Report
            // NoStoreRuntime when not owned so Restore can steer to the license
            // flow instead of blaming the network for an unretryable condition.
            return _isPro ? RefreshResult.Owned : RefreshResult.NoStoreRuntime;
        }

        if (LooksLikePlaceholder(_addonStoreId))
        {
            // No real Store ID yet — never grant Pro from a placeholder.
            SetIsPro(false);
            return RefreshResult.NotOwned;
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
            WriteCachedPro(owned);
            return owned ? RefreshResult.Owned : RefreshResult.NotOwned;
        }
        catch
        {
            // Store unavailable / network error / package not signed by Store.
            // Keep the previous (cached) value rather than flipping to free, so a
            // transient outage doesn't lose Pro for a purchaser mid-session.
            return RefreshResult.CheckFailed;
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
        if (_context is null)
        {
            LastError = "The Microsoft Store isn't available in this build.";
            return StorePurchaseStatus.NotPurchased;
        }
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
                // A successful purchase is itself proof of entitlement. Grant Pro
                // directly and cache it, so the user isn't left locked out if the
                // follow-up collection query lags or fails (it commonly lags right
                // after purchase). The refresh below reconciles when it can.
                SetIsPro(true);
                WriteCachedPro(true);
                await RefreshAsync().ConfigureAwait(false);
            }
            if (result.ExtendedError is { } err)
                LastError = $"HRESULT=0x{err.HResult:X8} {err.Message}";
            return result.Status;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message} (HRESULT=0x{ex.HResult:X8})";
            // Not necessarily a network fault (could be a COM/identity/HWND error),
            // but the raw detail is surfaced via LastError; return a generic error.
            return StorePurchaseStatus.ServerError;
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

    private bool ReadCachedPro()
    {
        try
        {
            return File.Exists(_cachePath)
                && File.ReadAllText(_cachePath).Trim() == CacheOwnedMarker;
        }
        catch
        {
            return false;
        }
    }

    private void WriteCachedPro(bool owned)
    {
        try
        {
            if (owned)
            {
                var dir = Path.GetDirectoryName(_cachePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_cachePath, CacheOwnedMarker);
            }
            else if (File.Exists(_cachePath))
            {
                // ReadCachedPro treats absence and empty identically, so on a
                // transition to not-owned (refund/downgrade) the file is dropped
                // rather than left as a zero-byte marker.
                File.Delete(_cachePath);
            }
        }
        catch
        {
            // Best-effort: without the cache we just fall back to querying the
            // Store next launch, which is the pre-cache behavior.
        }
    }

    // Advisory marker only; consistent with the deter-casual threat model of the
    // license-key channel. A tampered cache is corrected by the next successful refresh.
    private const string CacheOwnedMarker = "pro";

    private static bool LooksLikePlaceholder(string id)
        => string.IsNullOrWhiteSpace(id) || id.StartsWith("PLACEHOLDER_", StringComparison.Ordinal);
}
#endif
