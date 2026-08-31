# Microsoft Store Submission Checklist

> Walkthrough for actually publishing TimeBarX to the Microsoft Store. Code-side
> work for this is in commits on `main`; this file is the ops half — Partner
> Center clicks, listing copy, and the one-shot MSIX upload.

---

## Pre-flight (one-time, do these in parallel with Phases 1–4)

- [ ] **Create a Partner Center account** at <https://partner.microsoft.com/dashboard/registration> — Individual, free. Identity check (driver's license / passport upload) can take **24–72h**, sometimes longer; do this early.
- [ ] **Reserve the app name "TimeBarX"** in Partner Center → My apps → New product → MSIX or PWA app. The reservation is what gives you the Identity / Publisher values needed by `Package.appxmanifest`.
- [ ] **Host a privacy policy** — required because the app uses `StoreContext` for IAP. A simple GitHub Pages page is enough; template at the bottom of this doc.

## Identity wiring (10 min once Partner Center is up)

Partner Center → Your app → **App identity** → copy these three values:

| Partner Center field | Where it goes in `Package.appxmanifest` |
|---|---|
| Package/Identity/Name        | `<Identity Name="…">`            |
| Package/Identity/Publisher   | `<Identity Publisher="CN=…">`   |
| Package/Properties/PublisherDisplayName | `<PublisherDisplayName>…</PublisherDisplayName>` |

Replace the three `PLACEHOLDER_*` lines in `Package.appxmanifest`. Don't commit your real Publisher GUID if the repo is public — keep the placeholders on `main` and substitute at publish time, or commit a private branch.

## IAP add-on (the Pro unlock)

Partner Center → Your app → **Add-ons** → New add-on:

- **Product type**: Durable (one-time, never expires)
- **Product ID**: `TimeBarXPro` (this is your internal id; the Store ID is auto-generated)
- **Properties**:
  - Lifetime: **Forever**
- **Pricing**: USD **$4.99**. Pricing tiers handle conversions to other currencies automatically.
- **Listing**: see "IAP listing copy" below.
- **Submit and certify** — must be live *before* the main app submission references it.

After it's certified, Partner Center → Add-on → Properties → **Store ID** is the value you put into the app:

```csharp
// src/TimeBarX.App/Store/StoreEntitlements.cs
public const string ProStoreId = "9NXXXXXXXXXX"; // the Store ID from Partner Center
```

Or set the `TIMEBARX_PRO_STORE_ID` environment variable in the build pipeline so the constant stays a placeholder in the repo.

## Building the MSIX

On a Windows host with .NET 10 SDK + Windows 10/11 SDK installed:

```powershell
.\scripts\publish-msix.ps1
```

That produces `artifacts\msix\TimeBarX.msix`. Partner Center signs the package for you during certification — you don't need a code-signing cert for Store submissions. (You *do* need one if you also distribute the MSIX outside the Store.)

## Listing copy (paste into Partner Center)

### Short description (≤100 chars)

> A thin progress-bar timer pinned to the edge of every screen. Click-through, multi-monitor, never breaks focus.

### Description (≤10k chars)

> **TimeBarX is a timer that lives on the edge of your screen.**
>
> A thin always-visible progress bar pinned to the top of every monitor. Click-through, so it never gets in the way. No window to manage, no notifications to dismiss, no task list to maintain — just a quiet bar that fills as your timer runs.
>
> **Free, forever:**
> - Start, pause, resume, stop — preset durations from 1 minute to 90
> - Natural-language input: "25 min", "1:30", "45m standup"
> - Top-of-screen bar across every connected monitor
> - Global hotkey (Ctrl+Shift+T) for instant input
> - Completion effects and optional system sound
> - Survives sleep, wake, and reboot — your timer doesn't lose its place
>
> **Pro — one-time $4.99 unlock:**
> - Custom colors + gradient mode (green → red as time runs out)
> - Bottom / taskbar-fill position
> - "Always above everything" — keeps the bar over full-screen windows
> - Save your own named presets (Standup, Pomodoro, Lunch...)
> - `timebarx://` URI automation — wire it up with PowerToys Run, Flow Launcher, AutoHotkey
>
> No subscription. No account. No analytics. Your timer state lives in a JSON file on your disk. The Pro unlock is a one-time payment that you keep forever; if you change Microsoft accounts, "Restore Purchase" brings it back.
>
> Pairs well with the macOS TimeBarX menu-bar timer for cross-platform Pomodoro sessions.

### Keywords (≤7)

```
timer, pomodoro, focus, progress bar, productivity, countdown, hotkey
```

### What's new (per release)

> v0.1 — Initial release.

### IAP listing copy

**Title**: TimeBarX Pro
**Description**:
> Unlock TimeBarX Pro — a one-time $4.99 upgrade. Custom colors, gradient mode, bottom/taskbar-fill position, "Always above everything", URI automation for PowerToys / Flow / AutoHotkey, and saved custom presets. No subscription, no account, no analytics. Keep it forever.

## Visual assets

Generated already by ImageMagick (see `assets/store-tiles/`), referenced by `Package.appxmanifest`:

| Asset | Size | File |
|---|---|---|
| Store logo | 50×50 | `StoreLogo.png` |
| Small tile | 71×71 | `Square71x71Logo.png` |
| Medium tile / Square150 logo | 150×150 | `Square150x150Logo.png` |
| Large tile | 310×310 | `Square310x310Logo.png` |
| Wide tile | 310×150 | `Wide310x150Logo.png` |
| Square44 logo (taskbar/start) | 44×44 | `Square44x44Logo.png` |
| Splash screen | 620×300 | `SplashScreen.png` |

To regenerate from `assets/icon-master.png`:
```bash
# (commands embedded; see asset-generation note in PRO_PLAN.md)
magick assets/icon-master.png -resize 50x50  assets/store-tiles/StoreLogo.png
# … (see scripts/publish-msix.ps1 for the actual build flow)
```

### Screenshots (required, at least 1)

Partner Center accepts 1366×768 or larger. Plan for **4–6 screenshots**, captured at 1920×1080:

1. **Top-mode bar running** — the primary value proposition. Show a desktop with an in-progress blue bar across the top.
2. **Bottom / taskbar-fill mode** — the Pro marquee. Same screenshot trick: a partially-filled bar at the taskbar position.
3. **Tray menu open** — shows Start timer ▸ submenu with both built-in presets and a couple of custom presets ("Standup", "Pomodoro").
4. **Settings → Appearance** — gradient mode on, custom color chosen, "Pro" chips visible (or hidden on a Pro screenshot).
5. **Quick input window** — Ctrl+Shift+T popup with "25 min review PR" typed.
6. **Completion effect** — the flash/pulse moment at end of a timer.

## Age rating

Run the **age-rating questionnaire** in Partner Center → Properties → Age ratings. Expected outcome: **Everyone 3+** (no user-generated content, no data collection beyond the IAP purchase, no third-party ads).

## Pricing & availability

- **Base app**: Free
- **Markets**: All available markets (no exclusions)
- **Visibility**: Public

## Final checklist before "Submit"

- [ ] `Package.appxmanifest` placeholders replaced with real Partner Center values
- [ ] `StoreEntitlements.ProStoreId` (or `TIMEBARX_PRO_STORE_ID` build var) set to the real add-on Store ID
- [ ] IAP add-on certified and live
- [ ] `scripts/publish-msix.ps1` produces a clean `TimeBarX.msix`
- [ ] Manual smoke on a clean Windows VM: install MSIX → tray icon shows → Ctrl+Shift+T works → bar renders → Buy Pro flow opens the Store → after a sandbox purchase, Pro features unlock without restart
- [ ] Screenshots captured (4–6, 1920×1080)
- [ ] Description copy reviewed for typos
- [ ] Privacy policy URL live and reachable
- [ ] Submit. Cert review typically completes in **24–72h**; budget one resubmit cycle for first-time policy nits (description quality, screenshot framing).

## After "Live"

Phase 6 (`PRO_PLAN.md` §3) — promo codes, direct channel via Gumroad, ratings seeding.

---

## Privacy policy template

Save as e.g. `docs/privacy.md`, host on GitHub Pages at `https://<user>.github.io/timebarx/privacy`, link from Partner Center → Properties → Privacy policy URL.

```
# TimeBarX Privacy Policy

TimeBarX is a local-only timer utility. It does not collect, transmit, or share
any personal information.

## Data stored locally

The app reads and writes the following on your device only:

- Your timer state (in-progress, paused, completed) so it survives sleep/restart,
  stored in `%APPDATA%\TimeBarX\state.json`.
- Your settings (color, height, position, custom presets, etc.) in
  `%APPDATA%\TimeBarX\settings.json`.

These files never leave your machine.

## In-app purchase

The optional "TimeBarX Pro" unlock is processed by the Microsoft Store. Your
purchase status is queried from the Store on launch via the `Windows.Services.Store`
API. The app receives only whether you've purchased Pro — no payment data, no
account identifier — and caches that boolean locally. Refunds remove the
entitlement on the next launch.

## Updates

If you set the `TIMEBARX_UPDATE_URL` environment variable, the app fetches a
JSON file from that URL once at launch to check for newer versions. This is
opt-in and off by default.

## Contact

Issues and questions: <https://github.com/PLACEHOLDER/TimeBarX/issues>
```
