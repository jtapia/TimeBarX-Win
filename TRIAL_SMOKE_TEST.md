# TimeBarX Pro — 14-day trial smoke test

Run this on a **real Windows box** (10 1809+ or 11) after building the app.
Verifies the trial grants Pro on first launch, reverts cleanly after 14 days,
and shows the loss-aversion prompt exactly once. **Budget: 20 minutes.**

Unlike `PURCHASE_SMOKE_TEST.md`, this needs **no Microsoft account and no
payment** — the trial is enforced entirely by a local stamp file
(`%APPDATA%\TimeBarX\trial.json`), independent of the Store. Time-travel is
done by editing that file, so you never have to wait 14 real days.

## Ground truth (what the code does)

- On first launch with no `trial.json`, `TrialEntitlements` stamps
  `StartedUtc = now` (UTC) and grants Pro for `TrialWindow.DefaultLength`
  (**14 days**). See `src/TimeBarX.App/Store/TrialEntitlements.cs` +
  `src/TimeBarX.Core/TrialWindow.cs`.
- Pro is the OR of Store / license-key / trial (`CompositeEntitlements`), so an
  active trial unlocks **everything**: custom colors, gradient, always-above,
  URI automation, custom presets, and **multi-monitor** (the bar on every
  connected display).
- On expiry the app reverts to free and shows `UpgradeProDialog` **once**
  (`App.MaybeShowTrialExpiredPrompt`), reframed around what was lost. A
  persisted `ExpiryPromptShown` flag stops it ever nagging again.
- `trial.json` shape (System.Text.Json, PascalCase):
  ```json
  { "StartedUtc": "2026-09-01T12:00:00+00:00", "ExpiryPromptShown": false }
  ```

## Preconditions

- [ ] App built and runnable on the test box (MSIX or Inno build — either works;
      the trial is channel-independent).
- [ ] The test account is **not** already Pro via the Store or a license key —
      otherwise the composite reports Pro regardless of the trial and you can't
      observe reversion. If it is, sign into a clean account or remove
      `%APPDATA%\TimeBarX\license.txt`.
- [ ] Know the two paths:
  - Trial stamp: `%APPDATA%\TimeBarX\trial.json`
  - License (must be absent for this test): `%APPDATA%\TimeBarX\license.txt`

---

## Test 1 — First launch starts the trial, Pro is fully on

1. Ensure `%APPDATA%\TimeBarX\trial.json` does **not** exist (delete it if so).
2. Launch TimeBarX. Confirm the tray icon appears and a bar renders.
3. Right-click tray → **Settings…**. Confirm **no Pro lock chips** — Color,
   Gradient, Always-above are all interactive.
4. Pick a non-default color (e.g. Purple) and toggle Gradient. The bar should
   change — confirming Pro is genuinely active, not just unlocked visually.
5. Open `%APPDATA%\TimeBarX\trial.json`. Confirm it now exists with a
   `StartedUtc` at roughly the current UTC time and `"ExpiryPromptShown": false`.

**Pass:** first launch stamped the file and granted full Pro.
**If it fails:** the trial source isn't in the composite, or the file couldn't
be written (permissions). Check `TrialEntitlements` is passed to
`CompositeEntitlements` in `App.axaml.cs`.

## Test 2 — Multi-monitor is on during the trial (multi-monitor box only)

*(Skip if the test box has one display.)*

1. With the trial active from Test 1, confirm a bar renders on **every**
   connected monitor.

**Pass:** trial grants multi-monitor.
**If it fails:** `DisplayManager.TargetScreens()` isn't seeing `IsPro=true` from
the trial — same root cause as a Test 1 failure.

## Test 3 — Time-travel to expiry reverts to free

1. Quit TimeBarX (tray → Quit).
2. Edit `%APPDATA%\TimeBarX\trial.json`: set `StartedUtc` to **15 days ago**
   (any UTC instant more than 14 days before now). Leave `ExpiryPromptShown`
   as `false`. Save.
3. Relaunch TimeBarX.

Expected on launch:
- The bar reverts to the **free defaults**: blue, no gradient, Top position
  (Position is free so it stays, but Color/Gradient/Always-above clamp).
- On a multi-monitor box: secondary-monitor bars **disappear**; only the
  primary keeps its bar.
- Open Settings → the **Pro lock chips are back** on Color / Gradient /
  Always-above.

**Pass:** expiry cleanly downgraded to free without deleting the user's stored
Pro preferences (they're clamped, not wiped — see `ClampForEntitlement`).
**If it fails:** if it's *still* Pro, confirm the account isn't Pro via Store/
license (Precondition), and that `StartedUtc` really is >14 days back and parses
as UTC.

## Test 4 — The expiry prompt shows exactly once

1. Continuing from Test 3's expired state (or repeat step 2 with
   `ExpiryPromptShown: false`), relaunch.
2. Within a moment of startup, the **upgrade dialog** should appear, titled
   *"Your TimeBarX Pro trial ended"*, leading with what was lost (colors,
   gradient, multi-monitor).
3. Click **Not now** to dismiss.
4. Check `trial.json`: `"ExpiryPromptShown"` should now be **`true`**.
5. Quit and relaunch.

Expected: the prompt does **not** appear again.

**Pass:** prompt is strictly one-time.
**If the prompt never shows:** confirm `HasExpired` is true (StartedUtc >14 days
back), the account isn't otherwise Pro, and `ExpiryPromptShown` was `false`
before the relaunch in step 1.
**If it shows every launch:** `MarkExpiryPromptShown` isn't persisting — check
write permissions on `trial.json`.

## Test 5 — Owners of Pro never see the prompt

1. Grant Pro another way: either buy via the Store, or drop a valid key into
   `%APPDATA%\TimeBarX\license.txt` (or activate one via
   Settings → upgrade → "I have a license key…").
2. Set `trial.json` `StartedUtc` to 15 days ago, `ExpiryPromptShown: false`.
3. Relaunch.

Expected: **no** expiry prompt (the guard is `!IsPro`), and Settings shows Pro
unlocked (via the other channel).

**Pass:** the prompt is suppressed for paying users even with an expired trial.

## Test 6 — Live downgrade when Settings opens mid-session

1. Fresh trial (delete `trial.json`, launch — Pro active).
2. **Without quitting**, edit `trial.json` to set `StartedUtc` 15 days ago, save.
3. In the running app, open Settings → **…**.

Expected: `App.OpenSettings` calls `Trial.Refresh()`, which re-evaluates the
clock; the active→expired transition fires `Changed`, so the **Pro chips
re-lock live** and (multi-monitor box) secondary bars drop — all without a
restart.

**Pass:** a session that outlives the trial downgrades on the next Settings open.
**If it stays Pro:** `Trial.Refresh()` isn't wired into `OpenSettings`, or the
edited `StartedUtc` isn't actually past the window.

## Test 7 — Reset behavior (documented, accepted)

1. Delete `%APPDATA%\TimeBarX\trial.json` entirely.
2. Relaunch.

Expected: a **new** trial starts (Pro active again, fresh `StartedUtc`). This is
the accepted reset per `PRO_PLAN.md` §4 / §0 — not a bug. It's called out here
so a tester doesn't file it as one.

---

## Cleanup

- Delete `%APPDATA%\TimeBarX\trial.json` to leave the box in a first-run state,
  or set `ExpiryPromptShown: true` + a past `StartedUtc` to simulate a
  long-expired free user.
- Remove any test `license.txt` you added in Test 5.

## What this does NOT cover

- The Microsoft Store's own native "Free trial" listing mechanic — TimeBarX's
  trial is app-enforced and independent of it. If a Store-side free trial is
  ever configured in Partner Center, test that separately.
- Clock-rollback abuse — `TrialWindow` treats a future `StartedUtc` as
  just-begun (never expired-by-bad-clock), but deliberately doesn't fight a user
  who rolls their clock back to extend the trial. Accepted for a $5 app.
- The Store purchase flow itself — see `PURCHASE_SMOKE_TEST.md`.

## After a clean pass

- [ ] Note the build/version tested and the date.
- [ ] If any test failed, file it before shipping — the trial is the primary
      conversion lever, and a broken revert or a nagging prompt directly hurts
      ratings.
