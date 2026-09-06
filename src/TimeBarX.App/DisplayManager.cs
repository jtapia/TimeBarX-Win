using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform;

namespace TimeBarX.App;

/// <summary>
/// Owns one OverlayWindow per connected monitor and keeps them in sync with
/// physical display changes (connect/disconnect/resolution).
/// </summary>
public sealed class DisplayManager : IDisposable
{
    private readonly TrayController _controller;
    private readonly Dictionary<Screen, OverlayWindow> _overlays = new();
    private Screens? _screens;

    public DisplayManager(TrayController controller)
    {
        _controller = controller;
    }

    public void Start()
    {
        // A Window is required to access Screens. Promote the first overlay we
        // create into our managed set rather than spinning up a throwaway host.
        var first = CreateOverlay();
        first.Show();
        _screens = first.Screens;

        if (_screens is null)
        {
            // No screen info (headless / remote-session edge): don't leave the
            // shown, unpositioned, unmanaged window lingering until process exit.
            first.Close();
            return;
        }

        var primary = _screens.Primary ?? _screens.All.FirstOrDefault();
        if (primary is not null)
        {
            first.PositionOnScreen(primary);
            _overlays[primary] = first;
        }
        else
        {
            first.Close();
        }

        Rebuild();
        _screens.Changed += OnScreensChanged;

        // Multi-monitor is a Pro feature: buying Pro (or the trial expiring)
        // must add/remove the secondary overlays live, without a restart.
        _controller.Entitlements.Changed += OnEntitlementChanged;
    }

    private OverlayWindow CreateOverlay() => new() { DataContext = _controller };

    /// <summary>
    /// The screens that should currently host an overlay. Multi-monitor is
    /// Pro-only: free/expired-trial users get the primary screen only; Pro users
    /// get every connected screen.
    /// </summary>
    private IEnumerable<Screen> TargetScreens()
    {
        if (_screens is null) return Array.Empty<Screen>();
        if (_controller.Entitlements.IsPro) return _screens.All;

        var primary = _screens.Primary ?? _screens.All.FirstOrDefault();
        return primary is null ? Array.Empty<Screen>() : new[] { primary };
    }

    public void Stop()
    {
        _controller.Entitlements.Changed -= OnEntitlementChanged;

        if (_screens is not null)
        {
            _screens.Changed -= OnScreensChanged;
            _screens = null;
        }

        foreach (var window in _overlays.Values.ToList())
        {
            window.Close();
        }
        _overlays.Clear();
    }

    public void Dispose() => Stop();

    private void OnScreensChanged(object? sender, EventArgs e) => Rebuild();

    // Entitlement flips can arrive off the UI thread (e.g. a Store refresh
    // completing); overlay windows must only be created/closed on the UI thread.
    private void OnEntitlementChanged()
        => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        if (_screens is null) return;

        // The desired set is entitlement-gated: primary-only when not Pro, all
        // screens when Pro. Overlays for any screen outside this set are closed —
        // which disposes their OverlayPolicy (see OverlayWindow.OnClosed), so the
        // per-monitor fullscreen-detection poll loop doesn't leak on downgrade.
        var current = TargetScreens().ToList();

        foreach (var (screen, window) in _overlays.ToList())
        {
            if (!current.Contains(screen))
            {
                window.Close();
                _overlays.Remove(screen);
            }
        }

        foreach (var screen in current)
        {
            if (_overlays.TryGetValue(screen, out var existing))
            {
                existing.PositionOnScreen(screen);
                continue;
            }

            var window = CreateOverlay();
            window.Show();
            window.PositionOnScreen(screen);
            _overlays[screen] = window;
        }
    }
}
