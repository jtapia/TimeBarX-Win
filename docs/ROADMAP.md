# TimeBarX for Windows — Development Roadmap

---

# Epic 1 — Foundation

**Goal**

Build a working timer engine independent from the UI.

---

## Phase 1.1 — Project Setup

### TICKET-001 — Create Windows Solution

**Description**
- Create Avalonia + C# solution.
- Configure project structure.

**Acceptance Criteria**
- Project builds successfully.
- Empty window launches.

---

### TICKET-002 — Create Shared TimerEngine

**Description**

Implement:

- start()
- stop()
- pause()
- resume()

**Acceptance Criteria**
- Timer calculates remaining time correctly.

---

### TICKET-003 — Progress Calculation

**Description**

Calculate:

```text
elapsed / total
```

**Acceptance Criteria**
- Progress always stays between 0 and 1.

---

# Epic 2 — System Tray

**Goal**

Transform the app into a tray utility.

---

## Phase 2.1 — Tray Application

### TICKET-004 — Add Tray Icon

**Acceptance Criteria**
- Icon appears in system tray.
- No main window is visible.

---

### TICKET-005 — Create Tray Menu

Items:

- Start
- Pause
- Resume
- Stop
- Quit

---

### TICKET-006 — Display Remaining Time

Example:

```text
25:00 remaining
```

---

# Epic 3 — Overlay Infrastructure

**Goal**

Create the floating progress bar.

---

## Phase 3.1 — Single Monitor Overlay

### TICKET-007 — Create OverlayWindow

Properties:

- Borderless
- Transparent
- TopMost

---

### TICKET-008 — Position Overlay at Top Edge

Calculate:

```csharp
x = monitor.Left
y = monitor.Top
```

---

### TICKET-009 — Remove Taskbar Presence

**Acceptance Criteria**

- No Alt+Tab entry.
- No taskbar icon.

---

### TICKET-010 — Implement Click-Through Behavior

Allow mouse events to pass through.

---

# Epic 4 — Rendering

**Goal**

Draw the timer progress.

---

## Phase 4.1 — Basic Renderer

### TICKET-011 — Draw Solid Progress Bar

Example:

```text
████████░░░░░░
```

---

### TICKET-012 — Connect Renderer to TimerEngine

**Acceptance Criteria**

Progress updates in real time.

---

### TICKET-013 — Create Refresh Loop

Support:

- 30 FPS
- 60 FPS

---

# Epic 5 — Multi-Monitor Support

**Goal**

Mirror the macOS experience.

---

## Phase 5.1 — DisplayManager

### TICKET-014 — Detect Connected Monitors

Use:

```csharp
Screen.AllScreens
```

---

### TICKET-015 — Create Overlay per Monitor

Example:

```text
Overlay #1
Overlay #2
Overlay #3
```

---

### TICKET-016 — Synchronize Overlays

**Acceptance Criteria**

All monitors show identical progress.

---

# Epic 6 — Persistence

**Goal**

Never lose an active timer.

---

### TICKET-017 — Save Timer State

Store:

```json
{
  "endTime": "...",
  "preset": "25m"
}
```

---

### TICKET-018 — Restore Timer After Restart

**Acceptance Criteria**

Timer resumes correctly after relaunch.

---

### TICKET-019 — Handle Sleep/Wake Events

Support:

- suspend
- resume

---

# Epic 7 — Global Shortcut

**Goal**

Start timers from anywhere.

---

### TICKET-020 — Register Ctrl+Shift+T

---

### TICKET-021 — Create Quick Input Window

Examples:

```text
25 min
1:30
2h review PR
```

---

# Epic 8 — Completion Effects

**Goal**

Make timer completion noticeable.

---

### TICKET-022 — Pulse Effect

---

### TICKET-023 — Flash Effect

---

### TICKET-024 — Fade Effect

---

### TICKET-025 — Optional Completion Sound

---

# Epic 9 — Customization

**Goal**

Provide Pro-level customization.

---

### TICKET-026 — Color Picker

---

### TICKET-027 — Transparency Slider

---

### TICKET-028 — Height Selector

Options:

- 2 px
- 3 px
- 4 px

---

### TICKET-029 — Gradient Mode

```text
Green → Yellow → Red
```

---

# Epic 10 — Natural Language Input

**Goal**

Bring macOS input experience to Windows.

---

### TICKET-030 — Parse Durations

Examples:

```text
25 min
1h
2h 15m
1:30
```

---

### TICKET-031 — Parse Trailing Labels

Example:

```text
25 min review PR
```

---

# Epic 11 — Automation

**Goal**

Integrate with external tools.

---

### TICKET-032 — URI Scheme Support

Example:

```text
timebarx://start?duration=25m
```

---

### TICKET-033 — PowerToys Integration

---

### TICKET-034 — Flow Launcher Integration

---

### TICKET-035 — AutoHotkey Examples

---

# Epic 12 — Settings Window

**Goal**

Expose configuration options.

---

### TICKET-036 — General Tab

---

### TICKET-037 — Appearance Tab

---

### TICKET-038 — Shortcuts Tab

---

### TICKET-039 — About Tab

---

# Epic 13 — Always Above Everything (Experimental)

**Goal**

Keep overlays visible even in difficult scenarios.

---

### TICKET-040 — Experimental Mode Toggle

Default:

```text
OFF
```

---

### TICKET-041 — Aggressive TopMost Behavior

Attempt to remain visible above:

- fullscreen video
- borderless apps
- media players

---

### TICKET-042 — Compatibility Detection

Detect:

- fullscreen exclusive applications
- games

---

### TICKET-043 — Auto-Disable for Problematic Apps

Maintain configurable exclusion list.

---

### TICKET-044 — Experimental Warning Banner

Display:

```text
Experimental mode may not work with all applications.
```

---

# Epic 14 — Packaging

**Goal**

Prepare for public release.

---

### TICKET-045 — Application Icon

---

### TICKET-046 — Installer

Options:

- MSIX
- Inno Setup

---

### TICKET-047 — Auto-Updater

---

### TICKET-048 — Code Signing

---

# Epic 15 — Release Plan

## Beta 1

Includes:

- Timer engine
- Tray icon
- Single-monitor overlay

---

## Beta 2

Includes:

- Multi-monitor support
- Persistence

---

## Release Candidate

Includes:

- Natural language input
- Effects
- Customization

---

## v1.0

Includes:

- Automation
- Polished UI

---

## v1.1

Includes:

- Always Above Everything (Experimental)

---

# Recommended Development Order

1. TimerEngine
2. Tray icon
3. Single-monitor overlay
4. Multi-monitor support
5. Persistence
6. Natural language input
7. Customization
8. Automation
9. Always Above Everything (Experimental)

At the completion of **TICKET-015**, TimeBarX for Windows becomes usable and a closed beta can begin.
