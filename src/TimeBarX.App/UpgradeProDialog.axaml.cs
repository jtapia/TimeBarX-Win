using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TimeBarX.Core;
using TimeBarX.App.Store;

namespace TimeBarX.App;

/// <summary>
/// Single upgrade-flow window opened by any locked Pro control. Wires its Buy
/// button into the platform-appropriate entitlement source — real
/// <see cref="StoreEntitlements"/> on the Store target, a <see cref="MockEntitlements"/>
/// toggle on cross-platform/dev. Restore is a no-op-with-confirm in dev; on the
/// Store target it forces a refresh.
/// </summary>
public partial class UpgradeProDialog : Window
{
    private readonly IEntitlements _entitlements;
    private TextBlock? _status;
    private Control? _licensePanel;
    private TextBox? _licenseInput;
    private Button? _buyButton;
    private Button? _restoreButton;

    // Guards against a second Buy/Restore starting a concurrent Store operation
    // on the same StoreContext while one is already in flight.
    private bool _storeOpInFlight;

    /// <summary>
    /// Parameterless ctor only exists so the Avalonia XAML loader can resolve
    /// the resource at design time. Production code never uses it — defaults
    /// to a free entitlement so the dialog still renders without exploding.
    /// </summary>
    public UpgradeProDialog() : this(new FreeEntitlements()) { }

    public UpgradeProDialog(IEntitlements entitlements)
    {
        InitializeComponent();
        _entitlements = entitlements;
        _status = this.FindControl<TextBlock>("StatusText");
        _licensePanel = this.FindControl<Control>("LicensePanel");
        _licenseInput = this.FindControl<TextBox>("LicenseInput");
        _buyButton = this.FindControl<Button>("BuyButton");
        _restoreButton = this.FindControl<Button>("RestoreButton");
    }

    private bool BeginStoreOp()
    {
        if (_storeOpInFlight) return false;
        _storeOpInFlight = true;
        if (_buyButton is not null) _buyButton.IsEnabled = false;
        if (_restoreButton is not null) _restoreButton.IsEnabled = false;
        return true;
    }

    private void EndStoreOp()
    {
        _storeOpInFlight = false;
        if (_buyButton is not null) _buyButton.IsEnabled = true;
        if (_restoreButton is not null) _restoreButton.IsEnabled = true;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDismissClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void OnBuyClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
#if WINDOWS
        if (_entitlements is StoreEntitlements { IsStoreAvailable: false })
        {
            // Direct (non-MSIX) build: no Store runtime. Point the user at the
            // license-key flow that is the actual direct-channel purchase path.
            ShowStatus("This build unlocks Pro with a license key. Enter it below.");
            OnShowLicenseClicked(sender, e);
            return;
        }
        if (_entitlements is StoreEntitlements store)
        {
            if (!BeginStoreOp()) return;
            try
            {
                ShowStatus("Opening Microsoft Store…");
                var hwnd = TryGetHwnd();
                if (hwnd == IntPtr.Zero)
                {
                    ShowStatus("Purchase did not complete (no window handle available).");
                    return;
                }
                var status = await store.BuyAsync(hwnd).ConfigureAwait(true);
                if (status is Windows.Services.Store.StorePurchaseStatus.Succeeded
                    or Windows.Services.Store.StorePurchaseStatus.AlreadyPurchased)
                {
                    Close();
                    return;
                }
                var detail = store.LastError is { } err ? $"\n{err}" : "";
                ShowStatus($"Purchase did not complete ({status}).{detail}");
            }
            finally
            {
                EndStoreOp();
            }
            return;
        }
#endif
        if (_entitlements is MockEntitlements mock)
        {
            // Dev affordance: flip Pro on so we can exercise the Pro-only
            // gates on macOS / non-Store builds. Not reachable in Store builds
            // (the entitlement source is StoreEntitlements, not MockEntitlements).
            mock.SetPro(true);
            Close();
            return;
        }
        ShowStatus("Purchases aren't available in this build.");
    }

    private async void OnRestoreClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
#if WINDOWS
        if (_entitlements is StoreEntitlements store)
        {
            if (!BeginStoreOp()) return;
            try
            {
                ShowStatus("Checking Microsoft Store for prior purchase…");
                var result = await store.RefreshAsync().ConfigureAwait(true);
                switch (result)
                {
                    case RefreshResult.Owned:
                        Close();
                        return;
                    case RefreshResult.NotOwned:
                        ShowStatus("No prior purchase found on this Microsoft account.");
                        return;
                    default: // CheckFailed — don't claim "never purchased" on a failed query.
                        ShowStatus("Couldn't reach the Microsoft Store. Check your connection and try again.");
                        return;
                }
            }
            finally
            {
                EndStoreOp();
            }
        }
#endif
        await System.Threading.Tasks.Task.CompletedTask;
        ShowStatus("Restore is only available in the Microsoft Store build.");
    }

    private void OnShowLicenseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_licensePanel is null) return;
        _licensePanel.IsVisible = true;
        _licenseInput?.Focus();
    }

    private void OnActivateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var key = _licenseInput?.Text ?? string.Empty;
        if (Avalonia.Application.Current is not App app || app.LicenseKey is null)
        {
            ShowStatus("License activation isn't available in this build.");
            return;
        }
        if (app.LicenseKey.Activate(key))
        {
            Close();
            return;
        }
        ShowStatus("That key didn't verify. Check for typos or paste again from your purchase email.");
    }

    private IntPtr TryGetHwnd()
    {
        // Avalonia exposes the native window handle through the platform impl.
        // On Windows this is the top-level HWND that WinRT.Interop needs to
        // parent the Store purchase dialog. Returns Zero if the window hasn't
        // been shown yet (native handle isn't created until first show).
        var handle = TryGetPlatformHandle();
        return handle?.Handle ?? IntPtr.Zero;
    }

    private void ShowStatus(string text)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.IsVisible = true;
    }
}
