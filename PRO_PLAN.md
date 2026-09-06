# Plan: Free + Pro split, ship on Microsoft Store

> Companion to `MS_STORE.md`. Maps the recommended free/Pro split onto the
> existing code and Microsoft Store mechanics so we can land it incrementally.
> Each phase is independently shippable.

---

## 0. Decisions locked (closes open follow-ups from MS_STORE.md §7)

These shape the rest of the plan. **Decided** — no longer open. Revisit only with explicit cause.

| Decision | Locked answer | Rationale |
|---|---|---|
| **`timebarx://` URI automation** — Free or Pro? | **Pro** | Aligns with §5 list. Treats automation as a power-user upsell. Tradeoff acknowledged: gating it costs an evangelism surface; revisit post-launch if power-user adoption stalls. |
| **Multi-monitor** — Free or Pro? | **Pro** _(moved; was Free)_ | It's the functional gate that gives free users a real reason to pay. Free renders the bar on the **primary monitor only**; Pro spans **every connected display**. Single-monitor free stays fully usable, and the 7-day trial lets everyone experience multi-monitor before deciding. |
| **7-day Pro trial** — in scope? | **Yes — in scope** _(new)_ | First launch grants all Pro features free for 7 days, then reverts to free with a one-time "here's what you lost" upgrade prompt. Store supports it natively ("Free trial"); the app enforces it via a local trial stamp. Lets everyone feel multi-monitor + the cosmetic surface before the paywall. |
| **Bottom / taskbar-fill mode** — Free or Pro? | **Free** _(revised; was Pro)_ | Position (Top/Bottom) is a core placement choice, not a power-user extra. Kept free so the bar works where the user wants out of the box; the marquee taskbar-fill visual also helps sell the app in screenshots. |
| **Store SKU shape** — paid app + free SKU, or single free SKU with one IAP? | **Single free SKU + one durable IAP** | One listing accumulates reviews/ranking. Single non-consumable IAP "TimeBarX Pro" is the cleanest, matches the §3 economics (15% cut, ~$4.24 net per sale). |
| **Direct (non-Store) Pro unlock** — Gumroad license keys, Paddle, or honor-system? | **Defer to v1.1** | Ship Store first. Direct is §6's "found us via GitHub" funnel; not blocking launch. Phase 6 handles it. |
| **Launch promo** — free Pro for early reviewers? | **Yes — 50 codes, first 4 weeks** | Promo codes via Partner Center. Seeds ratings (§5 last bullet). Distribute to indie Windows blogs / r/Windows10 / r/productivity. |

---

## 1. Architecture: how "Pro-gated" works in code

**Single source of truth** — one `bool` the rest of the app checks: `Entitlements.IsPro`.

- **What it gates:** UI exposes Pro features always (no two-codepath split); enabling a Pro setting when `!IsPro` triggers the upgrade flow instead of mutating state. This keeps the codebase honest — no "free build" vs. "pro build" forks.
- **Where it lives:** new `TimeBarX.Core/Entitlements.cs` interface + a `StoreEntitlements` impl in `TimeBarX.App/Store/` that queries `Windows.Services.Store.StoreContext`. A `MockEntitlements` (env-var or settings flag override) is essential for local dev/test/CI.
- **State surface:** the Pro-gated fields already exist in `AppSettings`. Nothing new in `AppSettings`. The gate lives one layer up:

| Feature | Existing setting | Pro gate point |
|---|---|---|
| Custom/gradient colors | `Color`, `GradientMode` | `SettingsWindow` color radios past the default + gradient checkbox |
| Always above everything | `AlwaysAboveEverything` | `SettingsWindow` checkbox |
| `timebarx://` URIs | `UriCommand` | `App.HandleUri` short-circuits if `!IsPro` |
| Custom presets | _(none yet — design in Phase 4)_ | New "Manage presets…" dialog |
| Multi-monitor | _(display enumeration — no stored setting)_ | `DisplayManager` renders on the primary monitor only when `!IsPro`; not a `ClampForEntitlement` concern (it's a rendering decision, not a stored value) |
| Opacity fine-tuning | `Opacity` (slider) | _(open — see §0)_ |

**Free defaults must remain usable.** If a settings file from a Pro user is opened on a non-Pro install (e.g. machine swap before sync), the app must **not refuse to start**. The rule: Pro-only values silently clamp to free behavior at apply-time, but the *stored* value is preserved so re-purchase restores the full state. Add a `ClampForEntitlement(IsPro)` helper on `AppSettings` for this.

---

## 2. Microsoft Store IAP wiring (the heart of Phase 2)

This is the single biggest unknown, so call it out explicitly:

- The app needs the **`Windows.Services.Store`** SDK (`StoreContext.GetCustomerPurchasesAsync` / `RequestPurchaseAsync` for a durable add-on). Avalonia .NET 10 app can call WinRT APIs via `Microsoft.Windows.SDK.NET.Ref` on `net10.0-windows`. **Requires changing `TargetFramework` to `net10.0-windows10.0.19041.0`** (or similar) for the App project — Core stays cross-platform. Plan for the multi-target split.
- **Identity:** Store IAP requires the app to be **packaged** (MSIX) and signed by the Store. Won't work from the loose Inno Setup build. So the Store path is MSIX-only; the Inno installer keeps its own license-key check (Phase 5).
- **Add-on shape:** durable, non-consumable, in-app product titled "TimeBarX Pro" in Partner Center, with a Store ID assigned. The Store ID is what `StoreContext` queries.
- **Offline behavior:** `StoreContext` caches entitlements. App should treat "unknown" as "free" (fail closed) but check at startup + on `Settings…` open + after a successful purchase.

**Failure modes to handle:**
- No internet at first launch → Pro features locked until the next connected launch. UX: show "Sign in to Microsoft Store to verify Pro" toast in Settings.
- User refunds Pro → entitlement disappears. App must re-check periodically (every launch is enough); on revoke, gracefully downgrade (clamp + brief toast). Don't delete stored Pro preferences.

---

## 3. Phased delivery (smallest shippable steps)

### Phase 1 — Entitlement plumbing, no UI gates yet ✅ (commit `29647fc`)

- [x] Add `TimeBarX.Core/IEntitlements.cs` — `interface IEntitlements { bool IsPro { get; } event Action? Changed; }` + a `FreeEntitlements` default.
- [x] Add `TimeBarX.App/Store/MockEntitlements.cs` — env-var `TIMEBARX_PRO=1`/`true`/`yes` + a `SetPro(bool)` test hatch.
- [x] Wire it through `TrayController` as `public IEntitlements Entitlements { get; }` via a new 3-arg ctor (old ctors still work, default `FreeEntitlements`).
- [x] `AppSettings.ClampForEntitlement(bool)` returning a free-clamped view without mutating the stored record.
- [x] Tests in Core for `ClampForEntitlement` round-trip + no-data-loss (8 tests). Suite: 95/95 pass.
- [x] **No user-visible change.** Build green.

### Phase 2 — Store IAP integration ✅ (commit `cfe1526`)

- [x] Multi-target App project: `net10.0` (cross-platform dev/test) + `net10.0-windows10.0.19041.0` (Store target). `EnableWindowsTargeting=true` lets restore work on macOS.
- [x] `StoreEntitlements.cs` (Windows-only, `#if WINDOWS`): `StoreContext.GetUserCollectionAsync`; cached `IsPro`; `RefreshAsync()` + `BuyAsync()`.
- [x] `App` picks `StoreEntitlements` on Windows / `MockEntitlements` elsewhere via conditional compilation.
- [x] `Buy Pro…` entry in tray menu calls `BuyAsync()` on Windows or toggles `MockEntitlements` on dev.
- [x] Store ID configurable via `TIMEBARX_PRO_STORE_ID` env or the `ProStoreId` constant; placeholder is detected and forces `IsPro=false` until the real ID is dropped in.
- [ ] **Operational (Partner Center):** reserve the app name, create the durable add-on at $4.99, copy its Store ID into `StoreEntitlements.ProStoreId`. See `STORE_SUBMISSION.md`.

### Phase 3 — Apply the gates ✅ (commit `786c562`)

- [x] `TrayController.EffectiveSettings` (clamped view) consumed by `OverlayWindow.ApplySettings`/`ApplyBarColor`/`OnTimerCompleted` and `OverlayPolicy.SyncTimerToVisibility`/`Tick` — non-Pro renders Top/Blue/no-gradient regardless of stored values.
- [x] `Entitlements.Changed` forwarded onto `SettingsChanged` so flipping Pro live re-renders the overlay + Settings UI without restart.
- [x] `App.HandleUri`: silently no-ops all `timebarx://` commands when `!IsPro` (silent by design — automation runs unattended).
- [x] `SettingsWindow`: "Pro" lock chips on Color / Always-Above / Gradient (and later Presets; Position later made free). `RequireProOrPrompt` guard opens `UpgradeProDialog` and reverts the optimistic toggle.
- [x] `UpgradeProDialog`: single upgrade modal (Unlock $4.99 / Restore Purchase / Not now), wires `StoreEntitlements.BuyAsync` on Windows / `MockEntitlements.SetPro(true)` on dev.
- [x] **Tests**: round-trip restore confirms Pro values survive a free→Pro toggle. Suite: 96/96 pass.

### Phase 4 — Custom presets (the only genuinely new Pro feature) ✅ (commit `44049ba`)

- [x] `Core/CustomPreset.cs` record (`Name`, `Duration`, `Label?` + `IsValid` guard).
- [x] `AppSettings.CustomPresets: IReadOnlyList<CustomPreset>?` field; `Default` initializes to `Array.Empty<CustomPreset>()`; JSON round-trips cleanly.
- [x] `ManagePresetsDialog` (Settings → General → "Manage presets…") — CRUD list with Add/Remove. Uses `DurationParser.TryParse` so natural-language ("15 min", "45m standup") works in the duration field.
- [x] Tray "Start timer" submenu rebuilt in code on `SettingsChanged`/`Entitlements.Changed`. Pro + presets → presets above built-ins. Free → "Add custom preset… (Pro)" entry that opens the upgrade dialog.
- [x] 5 new tests for `CustomPreset` (validity, JSON round-trip, default shape). Suite: 101/101 pass.

### Phase 5 — Microsoft Store submission (1 day of paperwork, plus review wait) (1 day + ~24-72h)

**Code-side (this commit):**
- [x] **MSIX packaging plumbing:** `Package.appxmanifest` (with PLACEHOLDER identity), `scripts/publish-msix.ps1` that builds the self-contained Windows TFM and packs it with `makeappx`, `assets/store-tiles/*.png` (7 tiles generated from the master).
- [x] **`STORE_SUBMISSION.md`** — the operational checklist: listing copy, IAP setup walkthrough, screenshot plan, age-rating notes, final pre-submit checklist, privacy policy template.

**Operational (you do these in Partner Center):**
- [ ] **Account:** Create individual Partner Center account (free per §3). Identity check can take days; do this in parallel with Phase 1.
- [ ] **Reserve "TimeBarX"**, swap the three `PLACEHOLDER_*` lines in `Package.appxmanifest`.
- [ ] **Create the "TimeBarX Pro" durable IAP** at $4.99; copy its Store ID into `StoreEntitlements.ProStoreId` (or set `TIMEBARX_PRO_STORE_ID` in the build env).
- [ ] **Privacy policy URL** — required because of `StoreContext`. Template at the bottom of `STORE_SUBMISSION.md`; host on GitHub Pages.
- [ ] **Capture 4–6 screenshots** at 1920×1080 per the screenshot plan in `STORE_SUBMISSION.md`.
- [ ] **Age-rating questionnaire** — expected "Everyone 3+".
- [ ] **Pricing & availability:** Free, all markets.
- [ ] Submit, monitor certification (~24–72h; budget one resubmit cycle).

### Phase 6 — Launch promo + direct channel (post-launch, 1–2 days)

**Code-side (this commit):**
- [x] `LicenseKey` (Core): HMAC-SHA256 offline verifier, `TBX1-{payload}-{sig}` format, 11 unit tests covering round-trip, tamper, secret mismatch, case-insensitivity, paste tolerance.
- [x] `LicenseKeyEntitlements` (App): persists the key under `%APPDATA%\TimeBarX\license.txt`; `Activate(key)` / `Deactivate()`.
- [x] `OrEntitlements` combinator: Pro if Store OR License-key reports it. Composes Store/Mock + License so the channels coexist seamlessly.
- [x] "I have a license key…" entry in `UpgradeProDialog` (paste box + Activate button; closes on verify).

**Operational (see `DIRECT_CHANNEL.md`):**
- [ ] Change `LicenseKey.DefaultSecret` to a fresh production string.
- [ ] **Promo codes:** Partner Center → "Promotional codes" → generate ~50 codes tied to the Pro IAP. 4-week distribution plan in `DIRECT_CHANNEL.md`.
- [ ] **Gumroad:** product "TimeBarX Pro license" at $4.99 + webhook handler that calls `LicenseKey.Issue(payload, secret)` and emails the key.
- [ ] End-to-end smoke: buy via Gumroad, paste key into Upgrade dialog on a clean Win VM, verify Pro chips clear and stay cleared across restart.

---

## 4. Risks & mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| Store IAP plumbing is unfamiliar territory; Phase 2 could stall | **Medium** | Spike-prototype `StoreContext.GetCustomerPurchasesAsync` against a sandbox add-on **before** committing to the multi-target split. If Avalonia + Windows SDK ref proves painful, fallback: separate paid SKU instead of IAP (simpler, but lose freemium funnel). |
| MSIX packaging breaks the Avalonia tray-icon / global hotkey behavior | **Low-medium** | MSIX-packaged apps run with a different identity; some Win32 APIs behave differently. Test tray icon + Ctrl+Shift+T hotkey + `RegisterHotKey` from MSIX before submission. |
| Store certification rejection (description quality, screenshot policy) | **Medium** | Read [Store policies](https://learn.microsoft.com/en-us/windows/uwp/publish/store-policies) before first submit; first cert run usually flags 1-2 things — budget one resubmit cycle. |
| Refund-then-keep-Pro abuse | **Low** | Recheck entitlement every launch; on revoke, clamp settings. Acceptable loss for a $5 app. |
| "Pro modal" feels naggy / hurts ratings | **Medium** | Only shows on **intentional** clicks of locked controls, never as an interstitial. No persistent banner. Free tier never shows the modal on startup. |
| The "no internet → Pro locked" experience confuses paid users | **Medium** | Cache last-known entitlement for 30 days. Only mark Pro as "lost" if the Store *explicitly* says so, not on transient network errors. |

---

## 5. Definition of done (per phase)

- **Phase 1:** `dotnet test` green; `Entitlements` injectable; default impl returns false; env-var/dev override works on macOS dev box.
- **Phase 2:** real Store add-on round-trips a purchase in the Store sandbox; `Changed` fires; tray menu shows "Buy Pro…" on Windows only.
- **Phase 3:** all Pro-gated settings show lock chips + modal; clamping preserves stored values; free users cannot reach Pro behavior by editing JSON. Multi-monitor gated in `DisplayManager` — free renders the primary monitor only, Pro spans every display. The 7-day trial grants full Pro on first launch and reverts cleanly (via the local trial stamp) with the one-time upgrade prompt.
- **Phase 4:** custom presets persist, show in tray, behind Pro gate.
- **Phase 5:** Store listing live, app downloadable, IAP buyable end-to-end.
- **Phase 6:** at least 10 promo codes distributed; ≥ 3 organic reviews; direct license-key path documented.

---

## 6. Out of scope (intentionally)

- Subscription tier — explicitly rejected by `MS_STORE.md` §4.
- Cross-platform Pro (macOS already has its own TimeBarX; Pro entitlement does not cross stores).
- Server-side license validation — fully offline; HMAC-signed key for direct, `StoreContext` for Store.

> _Note: "a trial mode — the free tier is the trial" was previously out of scope. That's no longer true: a **7-day Pro trial** shipped (see §0) and is now in scope, enforced via a local trial stamp._
