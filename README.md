# TimeBarX for Windows

> A timer that lives on the edge of your screen.

A thin progress bar pinned to the top of every monitor. Always visible,
click-through, never breaks focus. The Windows counterpart to the macOS
TimeBarX menu-bar timer.

```
██████████████████████░░░░░░░░░░░░░░
```

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
timebarx://pause
timebarx://resume
timebarx://stop
```

See `INTEGRATIONS.md` for PowerToys / Flow Launcher / AutoHotkey examples.

---

## Run the tests

```sh
dotnet test
```

85+ unit tests cover the engine, parsers, persistence, color math, and
update-version comparison. The tests use a `FakeClock`, so they're
deterministic and fast (<1 s).

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

Delete either file to reset that aspect of the app.

---

## License

TBD.
