# TimeBarX for Windows

> A timer that lives on the edge of your screen.

A thin progress bar pinned to the top of every monitor. Always visible,
click-through, never breaks focus. The Windows counterpart to the macOS
TimeBarX menu-bar timer.

```
██████████████████████░░░░░░░░░░░░░░
```

---

## What's new in 1.0.6

Feature parity pass, migrating the most useful additions from the macOS
build:

- **Pomodoro cycle** — Work → Break → Work → Long-break, with configurable
  durations, long-break cadence (default: every 4 work sessions), and
  auto-advance. Start it from the tray or from Settings → Pomodoro.
- **Auto-pause on idle** — pause the running timer when the user has
  been idle at the OS level (no keyboard/mouse) for N minutes. Manual
  resume, same as macOS.
- **Session history** — completed timers are appended to
  `%APPDATA%\TimeBarX\history.jsonl` as one JSON object per line, safe
  to `type`, `tail`, or ingest into anything. Toggle in Settings → General.
- **Duration phrases** — the quick-input and `timebarx://` URIs now
  understand "an hour and a half", "2 hours and 30 minutes", "an hour
  and 15", "a hour and a quarter" alongside the existing `25m`, `1:30`,
  `half hour`.
- **Completion-sound picker** — pick among Asterisk, Beep, Exclamation,
  Hand, Question, or Off. Existing "play sound" checkboxes upgrade
  cleanly — nobody who had sound on loses it.
- **Per-preset overrides (plumbing)** — custom presets can carry their
  own completion sound and alert message via `settings.json`. The Add
  UI still only takes name + duration; a picker for the overrides lands
  in a follow-up.
- **URI path-form** — `timebarx:/start?duration=25m` (single slash) now
  works alongside `timebarx://start?…` for external launchers that drop
  the double slash.

---

## Repo layout

```
src/TimeBarX.Core   — platform-independent timer, parsers, settings (no UI)
src/TimeBarX.App    — Avalonia tray app, overlay windows, hotkey, URI handler
tests/              — xUnit tests for Core
scripts/            — publish.ps1, installer.iss
assets/             — icon.svg
```

See `PLAN.md` for the design, `ROADMAP.md` for the ticket map, and
`RELEASES.md` for milestone mapping.

---

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` ≥ 10.0). macOS install:
  `brew install dotnet`. Windows: <https://dot.net/download>.

That's it for development. Packaging the installer needs **Inno Setup 6**
on a Windows host (`scripts/installer.iss`).

---

## Run from source

From the repo root:

```sh
dotnet run --project src/TimeBarX.App
```

The tray app launches with no main window. On Windows you'll see a tray
icon; on macOS the menu appears in the system bar (overlay rendering and
the global hotkey are Windows-only — the app builds and the timer engine
runs everywhere, but the bar and Ctrl+Shift+T only light up on Windows).

### Try it

1. Right-click the tray icon → **Start 25 min**. A thin blue bar fills
   the top edge of every monitor.
2. **Ctrl + Shift + T** (Windows only) opens the quick input. Type
   `30s test` and press Enter for a fast smoke test.
3. Use **Appearance →** in the tray menu, or open **Settings…**, to try
   color, height, opacity, and gradient.
4. Right-click → **Stop** or wait for the completion flash / pulse / fade.

URI scheme (Windows only, after installer registers it):

```
timebarx://start?duration=25m
timebarx://start?duration=1:30&label=focus
timebarx://start?duration=an%20hour%20and%20a%20half
timebarx://pause
timebarx://resume
timebarx://stop
```

Path-form (single slash) works too, for launchers that strip the `//`:

```
timebarx:/start?duration=25m
```

See `INTEGRATIONS.md` for PowerToys / Flow Launcher / AutoHotkey examples.

---

## Run the tests

```sh
dotnet test
```

166 unit tests cover the engine, parsers, persistence, color math,
Pomodoro cadence, session history, and update-version comparison. The
tests use a `FakeClock`, so they're deterministic and fast (<1 s).

---

## Build a self-contained .exe (Windows)

The published EXE bundles the .NET runtime — users don't need to install
anything.

From a Windows host with the .NET 10 SDK:

```powershell
pwsh scripts/publish.ps1
```

Output: `artifacts/publish/TimeBarX.App.exe` (~70 MB). Double-click to
launch — no install required.

Override defaults if needed:

```powershell
pwsh scripts/publish.ps1 -Configuration Release -Runtime win-x64 -OutDir C:\out
```

### Cross-build from macOS / Linux

`dotnet publish` runs anywhere. Same one-liner:

```sh
dotnet publish src/TimeBarX.App/TimeBarX.App.csproj \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o artifacts/publish
```

Copy `artifacts/publish/TimeBarX.App.exe` to a Windows machine and run it.

---

## Build the installer (Windows only)

Generates a signed, registry-aware installer that also registers the
`timebarx://` URI scheme and (optionally) sets the app to launch at sign-in.

1. Publish the EXE: `pwsh scripts/publish.ps1`
2. Generate `assets/icon.ico` from `assets/icon.svg` — see
   `assets/README.md`.
3. Compile: `iscc scripts/installer.iss`

Output: `artifacts/installer/TimeBarX-<version>-Setup.exe`.

For production distribution you should also sign both the EXE and the
installer — full walkthrough in `SIGNING.md`. The full release recipe is
in `RELEASING.md`.

---

## Where things live at runtime

- Timer state: `%APPDATA%\TimeBarX\state.json`
- Settings: `%APPDATA%\TimeBarX\settings.json`
- Session history: `%APPDATA%\TimeBarX\history.jsonl` (JSONL, one entry
  per line)

Delete any file to reset that aspect of the app.

---

## License

TBD.
