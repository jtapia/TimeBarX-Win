# Changelog

All notable changes to TimeBarX are documented in this file. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Cross-platform `TimerEngine` (start/pause/resume/stop, progress 0..1).
- Avalonia tray app with Start / Pause / Resume / Stop / Quit menu and
  remaining-time tooltip.
- Borderless, transparent, click-through `OverlayWindow` pinned to the top
  edge of every connected monitor; rebuilds on display hot-plug.
- Solid progress bar driven by the timer at ~30 FPS.
- Persistent timer state across restart and sleep/wake (`JsonTimerStore`,
  `PowerEventBridge`).
- Global Ctrl+Shift+T hotkey and quick-input window with natural-language
  duration parsing (`25 min`, `1:30`, `2h review PR`, `half hour`,
  `quarter hour`).
- Completion effects: white flash, three opacity pulses, 2 s fade-out, and
  an optional Windows system sound.
- Settings: color preset, height (2/3/4 px), opacity, gradient mode,
  completion sound, default duration. Persisted to JSON, applied live to
  every overlay.
- Tray submenus and a tabbed Settings window (General, Appearance,
  Shortcuts, About) for changing settings.
- `timebarx://` URI scheme (`start?duration=…&label=…`, `pause`, `resume`,
  `stop`), single-instance forwarding via a named pipe, and integration
  examples for PowerToys, Flow Launcher, AutoHotkey, Windows Shortcuts.
- Experimental "Always above everything" mode with aggressive top-most
  reassertion, exclusive-fullscreen detection, and an auto-hide exclusion
  list for media players.
- Packaging: SVG icon source, `scripts/publish.ps1`, Inno Setup installer
  that registers the URI scheme, `SIGNING.md` walkthrough, and an
  opt-in background update check (`TIMEBARX_UPDATE_URL`).
