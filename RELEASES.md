# TimeBarX Release Plan

Each milestone is a gating point, not a development phase. The phases that
delivered each milestone's contents are listed in parentheses.

---

## Beta 1 — Tray + Single Monitor

Ships:

- Timer engine, tray icon, tray menu, remaining time
- Single-monitor overlay with click-through

Delivered by: Phase 1 (engine), Phase 2 (tray), Phase 3 (overlay window),
Phase 4 (rendering).

Status: **Ready** — exercise via `dotnet run --project src/TimeBarX.App`.

---

## Beta 2 — Multi-monitor + Persistence

Ships everything in Beta 1 plus:

- One overlay per connected monitor, hot-plug rebuild
- Timer state survives restart and sleep/wake

Delivered by: Phase 5 (DisplayManager), Phase 6 (JsonTimerStore + sleep/wake).

Status: **Ready**.

---

## Release Candidate — Natural Language + Polish

Ships everything in Beta 2 plus:

- Ctrl+Shift+T quick input (`25 min`, `1:30`, `2h review PR`, `half hour`)
- Completion effects (flash, pulse, fade, optional sound)
- Customization (color, height, opacity, gradient)
- Settings window

Delivered by: Phase 7 (hotkey + input), Phase 8 (effects), Phase 9
(customization), Phase 10 (NL parsing + labels), Phase 12 (settings UI).

Status: **Ready**.

---

## v1.0 — Automation + Polished UI

Ships everything in RC plus:

- `timebarx://` URI scheme, single-instance forwarding
- PowerToys / Flow Launcher / AutoHotkey integration docs
- App icon, Inno Setup installer with URI registration
- Code-signing pipeline, update-check stub

Delivered by: Phase 11 (automation), Phase 14 (packaging).

Status: **Ready** — production cut blocked only on a signing certificate
and a hosted versions.json endpoint.

---

## v1.1 — Always Above Everything (Experimental)

Ships everything in v1.0 plus:

- Opt-in aggressive top-most mode
- Exclusive-fullscreen detection
- Auto-hide for known media-player processes
- Warning banner in settings

Delivered by: Phase 13.

Status: **Ready** — toggle defaults OFF.

---

# Cut a Release

1. Update `<Version>` in `src/TimeBarX.App/TimeBarX.App.csproj`.
2. Move `CHANGELOG.md`'s Unreleased entries under the new version heading.
3. Follow `RELEASING.md` for publish / sign / installer / verify.
4. Tag: `git tag vX.Y.Z && git push --tags`.
5. Attach the signed installer to the GitHub release.
