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

    public IReadOnlyCollection<OverlayWindow> Overlays => _overlays.Values;

    public void Start()
    {
        // A Window is required to access Screens. Promote the first overlay we
        // create into our managed set rather than spinning up a throwaway host.
        var first = CreateOverlay();
        first.Show();
        _screens = first.Screens;

        if (_screens is null) return;

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
    }

    private OverlayWindow CreateOverlay() => new() { DataContext = _controller };

    public void Stop()
    {
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

    private void Rebuild()
    {
        if (_screens is null) return;

        var current = _screens.All.ToList();

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
