# TimeBarX Pro — Store purchase smoke test

Run this once after the app + Pro add-on show "In the Store" in Partner
Center. Verifies the end-to-end purchase flow works before you tell anyone
the app exists. **Budget: 15 minutes.** Requires a real Microsoft account
and a real payment method — Microsoft doesn't offer a sandbox for
StoreContext IAP purchases against a live-published add-on.

## Preconditions

- [ ] Partner Center → **TimeBarX** app status: `In the Store`.
- [ ] Partner Center → **Add-ons → TimeBarX Pro** status: `In the Store`.
- [ ] Add-on **Store ID** shown in Partner Center matches the constant in
      `src/TimeBarX.App/Store/StoreEntitlements.cs:36` (`ProStoreId = "9P80PM9PK9ND"`).
      If they differ, the Buy button will silently fail with `NotPurchased`.
- [ ] Payout profile: `Active`. Tax profile: `Approved`. If either is not
      approved, purchases still complete but revenue accrues without
      paying out — fine to test with, but fix before wider release.
- [ ] Test machine: **Windows 10 1809+ or Windows 11**, real hardware or
      VM, signed into a Microsoft account that is NOT your developer
      account (a real customer account — you refund yourself at the end).
- [ ] $5 available on a card attached to that MS account.

## Test 1 — Store install path

1. On the test machine, open the **Microsoft Store** app.
2. Search for `TimeBarX`. It should appear as a free app by "Eduardo Tapia."
3. Click **Get** / **Install**. Wait for install to complete.
4. Launch from Start menu. Confirm the tray icon appears.
5. Right-click tray → **Start 25 min**. Confirm the bar renders across the
   top of the monitor.

**Pass criteria:** app installed from Store, tray + bar work.
**If it fails:** the parent app package has a runtime problem. Fix the
MSIX before proceeding — no point testing IAP against a broken app.

## Test 2 — Buy flow (the money-critical path)

1. In the running app, right-click tray → **Settings…**.
2. Click any locked (Pro-chip) control — e.g. the **Purple** color radio,
   or the **Gradient** toggle. The upgrade dialog should open.
3. Click **Buy TimeBarX Pro**.

Expected: within ~2s, the Microsoft Store purchase dialog opens showing:
- **Product name:** TimeBarX Pro
- **Price:** $4.99 USD (or your local equivalent)
- **Publisher:** Eduardo Tapia

4. Complete the purchase with a real payment method.
5. After the Store dialog closes, the upgrade dialog should auto-close
   (`UpgradeProDialog.axaml.cs:53` — `Close()` on `Succeeded` /
   `AlreadyPurchased`).
6. Within a couple seconds, the previously locked controls should
   **unlock** — Pro chips disappear, color radios enable, gradient toggle
   becomes interactive. Driven by `Entitlements.Changed → SyncProChips`
   in `SettingsWindow.axaml.cs:106`.

**Pass criteria:** dialog shows the right price and product name, purchase
completes, app flips to Pro without a restart.

**If Buy button does nothing (no Store dialog appears):**
- The add-on Store ID is wrong, or the add-on isn't yet `In the Store`.
- Check `%LOCALAPPDATA%\Packages\EduardoTapia.TimeBarX_xwsez2c29kra6\LocalCache\` and
  Event Viewer → Windows Logs → Application for `.NET Runtime` errors.
- Try setting `TIMEBARX_PRO_STORE_ID` env var to the exact Store ID from
  Partner Center and relaunch. If that fixes it, update the constant.

**If dialog says "This product is unavailable":**
- Add-on visibility is set to `Stop acquisition`, or markets exclude your
  test account's country. Fix in Partner Center → add-on → Pricing and
  availability.

**If purchase succeeds but app stays locked:**
- `StoreEntitlements.SetIsPro` didn't fire `Changed`, or the marshaling
  to the UI thread failed. Restart the app — if Pro is now active, it's
  a live-refresh bug, not a purchase bug. File it, but Pro is delivered.

## Test 3 — Persistence across restart

1. With Pro now active, right-click tray → **Quit**.
2. Relaunch from Start menu.
3. Open Settings. Verify Pro-only controls are still unlocked.

**Pass criteria:** no re-purchase needed. `StoreEntitlements` ctor fires
`RefreshAsync` on construction; user's collection includes the add-on.

## Test 4 — Restore on a fresh install

1. Uninstall TimeBarX via Settings → Apps.
2. Reinstall from the Microsoft Store.
3. Launch.
4. Open Settings → click a locked control → in the upgrade dialog, click
   **Restore purchases**.

Expected: status text flips to "Checking Microsoft Store for prior
purchase…" then the dialog closes and controls unlock.

**Pass criteria:** Pro entitlement restored without paying again.

## Test 5 — Cross-device (optional but recommended)

If you have a second Windows machine or VM:
1. Sign in with the **same** Microsoft account.
2. Install TimeBarX from the Store.
3. Launch → open Settings.

Expected: Pro is already unlocked on first launch, because
`GetUserCollectionAsync` returns the add-on for that account across
devices.

**Pass criteria:** no explicit Restore needed — the entitlement follows
the account.

## Test 6 — Store metrics reflect the sale

Roughly 6–24h after the purchase in Test 2:
1. Partner Center → **Analytics** → **Acquisitions**.
2. Filter by add-on. Confirm one paid acquisition.
3. Partner Center → **Analytics** → **Revenue**. Confirm ~$4.24 credited
   (85% of $4.99, minus any tax withheld per your tax profile).

**Pass criteria:** the sale shows up. If it doesn't after 48h, open a
Partner Center support ticket — data missing from analytics is almost
always a reporting delay, not a lost sale.

## Cleanup — refund yourself

Microsoft allows self-refunds within 14 days, no questions:
1. Go to <https://account.microsoft.com/billing/orders>.
2. Find the TimeBarX Pro order.
3. Click **Request a refund** → pick "Purchased by mistake."
4. Refund typically approves in <1h and reverses within 3–5 business days.

The refund will also show up in Partner Center analytics with a negative
adjustment. This confirms the refund path works for real customers too.

## What this does NOT verify

- Sandbox purchases (the Microsoft Store sandbox for IAP is on the way
  out; the modern approach is testing against your live add-on with real
  money and refunding). If you need sandbox, look into "flights" and
  private preview groups.
- License-key channel (`LicenseKeyEntitlements`) — that's the direct
  Gumroad path, not the Store path. Separate smoke test.
- Multi-user on the same device — `StoreContext.GetDefault()` returns the
  currently-signed-in user; a second user on the same PC gets their own
  entitlement scope, which is correct.

## After a clean pass

- [ ] Take a screenshot of the completed purchase dialog (the confirmation
      page after payment) — useful for the Store listing later.
- [ ] Take a screenshot of the app with a Pro color selected — useful for
      Reddit posts and the Store listing.
- [ ] Post to r/csharp / r/windows / r/SideProject per `REDDIT.md`.
