# TimeBarX for Windows — Overlay Architecture

## Vision

TimeBarX for Windows should preserve the same core principle as the macOS version:

> Always visible. Never distracting.

Instead of trying to recreate the Mac menu bar, the Windows version should deliver the same benefit:

- Always know how much time is left.
- Never switch windows.
- Never break focus.

---

# Core Experience

A thin progress bar (2–3 px high) is displayed at the top edge of every connected monitor.

Example:

```
██████████████████████░░░░░░░░░░░░░░
```

Characteristics:

- Visible at all times
- Click-through
- Transparent background
- Borderless
- Lightweight
- Synchronized across multiple monitors

---

# Recommended Technology

## Preferred: Avalonia + C#

Advantages:

- Native Windows experience
- Multi-monitor support
- Overlay-friendly
- Future Linux support possible
- Easier long-term maintenance

Alternative:

- WinUI 3 (Windows only)

---

# Architecture

## TimerEngine

Platform-independent logic:

- startTime
- endTime
- remainingTime
- progress
- pause/resume
- presets
- persistence

No UI code.

---

## DisplayManager

Responsible for:

```csharp
Screen.AllScreens
```

Creates one overlay window per monitor.

```
DisplayManager
 ├── OverlayWindow #1
 ├── OverlayWindow #2
 └── OverlayWindow #3
```

---

## OverlayWindow

Each monitor gets its own overlay.

### Size

```text
width = monitor.width
height = 2–3 px
x = monitor.left
y = monitor.top
```

### Window properties

- Borderless
- Transparent background
- TopMost
- No focus
- Click-through
- No taskbar icon
- No Alt+Tab entry

---

# Rendering

## Solid Color

```
██████████░░░░░░░░░░░
```

## Gradient Mode

```
Green → Yellow → Orange → Red
```

## Smooth Color Interpolation

```csharp
Color.Lerp(startColor, endColor, progress)
```

---

# Animations

### Linear

```
██████████░░░░░░░░
```

### Smooth (60 FPS optional)

### Pulse on Completion

Opacity:

```
100%
40%
100%
```

### Flash Effect

Entire bar briefly flashes.

---

# Settings

## Height

- 2 px
- 3 px
- 4 px

## Color

- System Accent
- Blue
- Purple
- Green
- Red

## Transparency

0–100%

## Position

- Top
- Bottom (future)

---

# Visibility Modes

---

## Mode 1 — Normal (Default)

Optimized for daily work.

Visible above:

- VS Code
- Chrome
- Slack
- Word
- Photoshop
- Figma
- Maximized windows
- Borderless fullscreen video

Automatically hides when:

- Exclusive fullscreen games
- Applications taking direct control of rendering

Advantages:

- Maximum compatibility
- Lowest risk
- Best battery usage

---

## Mode 2 — Always Above Everything (Experimental)

For users who want the timer visible under almost all circumstances.

Attempts to remain visible above:

- Maximized windows
- Borderless fullscreen apps
- Most games
- Media players

May require:

- Additional window styles
- More aggressive TopMost behavior

### Risks

- Some anti-cheat systems may dislike overlays
- Potential compatibility issues with games
- Increased complexity
- Unexpected behavior on some systems

### Warning

This mode should be clearly labeled:

> Experimental — may not work with all applications.

Default: OFF

---

# Tray Icon

Equivalent to the macOS menu bar icon.

Menu:

```
TimeBarX
25:00 remaining

Pause
Resume
Stop

Settings
Quit
```

---

# Global Shortcut

Default:

```
Ctrl + Shift + T
```

Opens quick input:

```
25 min
1:30
2h review PR
```

---

# Persistence

Store:

```json
{
  "endTime": "...",
  "preset": "25m"
}
```

Recovery:

```text
remaining = endTime - now
```

Supports:

- Sleep
- Restart
- Application relaunch

---

# Multi-Monitor Support

All monitors stay synchronized.

Example:

Monitor 1:

```
██████████████░░░░░░░░
```

Monitor 2:

```
██████████████░░░░░░░░
```

Monitor 3:

```
██████████████░░░░░░░░
```

Single timer.
Multiple displays.

---

# Automation

Future support:

- URI schemes
- PowerToys Run
- Flow Launcher
- AutoHotkey
- Windows Shortcuts

---

# Roadmap

## V1

- Timer engine
- Tray icon
- Single-monitor overlay
- Global shortcut

## V2

- Multi-monitor support
- Persistence

## V3

- Colors
- Transparency
- Completion effects

## V4

- Natural language parsing

Examples:

```
25 min
1:30
2h review PR
```

## V5

- Automation integrations

## V6

- Always Above Everything (Experimental)

---

# Marketing

Avoid:

> Menu Bar Timer

Use:

### Option 1

> A timer that lives on the edge of your screen.

### Option 2

> Always know how much time is left without switching windows.

### Option 3

> Stay aware of time without breaking focus.

---

# Result

## macOS

```
Menu Bar
████████████████
```

## Windows

```
Top Screen Edge
████████████████
```

Different implementation.

Same experience.

Same identity.
