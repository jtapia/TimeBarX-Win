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
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDismissClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void OnBuyClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
#if WINDOWS
        if (_entitlements is StoreEntitlements store)
        {
            ShowStatus("Opening Microsoft Store…");
            var status = await store.BuyAsync().ConfigureAwait(true);
            if (status is Windows.Services.Store.StorePurchaseStatus.Succeeded
                or Windows.Services.Store.StorePurchaseStatus.AlreadyPurchased)
            {
                Close();
                return;
            }
            ShowStatus($"Purchase did not complete ({status}).");
            return;
        }
#endif
        if (_entitlements is MockEntitlements mock)
        {
            // Dev affordance: flip Pro on so we can exercise the Phase 3 gates
            // on macOS / non-Store builds. Not shipped to Store users.
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
            ShowStatus("Checking Microsoft Store for prior purchase…");
            await store.RefreshAsync().ConfigureAwait(true);
            if (_entitlements.IsPro)
            {
                Close();
                return;
            }
            ShowStatus("No prior purchase found on this Microsoft account.");
            return;
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

    private void ShowStatus(string text)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.IsVisible = true;
    }
}
