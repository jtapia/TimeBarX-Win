# Direct (Gumroad) Channel + Launch Promo

> Phase 6 ops doc. Code-side scaffolding is in commits on `main`; this is the
> half that lives in Gumroad and Partner Center dashboards.

---

## What's already done in code

- `LicenseKey` (Core) — HMAC-SHA256 verification, `TBX1-{payload}-{sig}` format. Pure / fully testable; 11 tests cover round-trip, tamper detection, secret mismatch, base32 case-insensitivity, paste tolerance.
- `LicenseKeyEntitlements` (App) — loads `%APPDATA%\TimeBarX\license.txt` at startup, exposes `Activate(key)` / `Deactivate()`. Implements `IEntitlements`.
- `OrEntitlements` (App) — composes Store/Mock + License-key so either channel grants Pro.
- `App.LicenseKey` — wired into the composite entitlement at startup. Available across the app.
- "I have a license key…" entry on `UpgradeProDialog` — paste box + Activate button that calls `LicenseKey.Activate`. Closes on success; shows "didn't verify" status on failure.

## What you do operationally

### 1. Set a production signing secret (one-time)

The code ships with `LicenseKey.DefaultSecret = "timebarx-direct-channel-v1"`. **Change this before publishing.** The secret isn't a cryptographic secret in the classic sense (anyone who pulls the EXE apart will find it), but it should be unique to your app so general-purpose crackers don't have a published target. To set it:

1. Edit `src/TimeBarX.Core/LicenseKey.cs`:
   ```csharp
   public const string DefaultSecret = "<your-fresh-secret-string>";
   ```
2. Rotate the secret only at major versions — every existing key signed with the old secret becomes invalid.

If you'd rather keep the secret out of git: have the publish script substitute it at build time from an env var, and commit the placeholder.

### 2. Build the key-issuer

Gumroad fires a webhook on every successful purchase. The webhook handler:

- Verifies the Gumroad request signature.
- Generates a license key with `LicenseKey.Issue(payload, secret)` — payload is the buyer's email or order ID (short, opaque).
- Emails the buyer their key (Gumroad's own product receipt is the easiest place; configure it under Product → Content → Customizable receipt).

The simplest implementation is **a single CLI** you run on a small server (or as a Cloudflare Worker):

```csharp
// scripts/issue-key/Program.cs (sketch)
using TimeBarX.Core;
var payload = args[0];        // e.g. "order-12345" or buyer email
var secret  = Environment.GetEnvironmentVariable("TIMEBARX_LICENSE_SECRET")!;
Console.WriteLine(LicenseKey.Issue(payload, secret));
```

For a pure-serverless flow, port the signing math to JavaScript and run it inside the Gumroad webhook (the algorithm is HMAC-SHA256, the format is base32 of payload and the first 10 bytes of the MAC).

### 3. Set up Gumroad

1. Create the product **"TimeBarX Pro license"** at **$4.99**.
2. Product type: digital, no file (the value delivered is the key, not a download).
3. Content → Customizable receipt → include your key in the email body. If you generate keys via webhook, configure the webhook URL and use Gumroad's templating to embed the returned key.
4. Gumroad's revenue cut: **~10% + fees**, vs. Microsoft Store's 15% — direct comes out marginally better for the seller but adds checkout friction (Gumroad's checkout is one extra step vs. the in-app Store purchase). Direct is the channel for users who found you via GitHub/README, not the primary funnel.

### 4. Promotion of the direct channel

- README link: "Buy direct via Gumroad" alongside "Get it on the Microsoft Store" badge.
- Mention in the in-app upgrade dialog? — **No.** The dialog points at the Store; the license-key textbox is hidden until the user clicks "I have a key…". Keeping the direct channel discoverable but unobtrusive avoids confusion for the 95% of users who'll buy through the Store.

---

## Launch promo (Microsoft Store side)

The plan: 50 free Pro codes to seed reviews.

### Generate codes

Partner Center → Your app → **Promotional codes**:

1. New code → tied to the **"TimeBarX Pro"** add-on (not the base app — base is free).
2. Quantity: **50**.
3. Expiry: **4 weeks** from launch.
4. Download the CSV. Each row is a redeemable code + Microsoft Store URL (the URL is what you share — clicking it auto-applies the code).

### Distribution targets (4-week schedule)

| Week | Target | Why |
|---|---|---|
| Launch day | r/Windows10, r/productivity, r/getdisciplined | Subreddits that index Win utilities; small focused communities, not the megasubs. Post once each (rules vary). |
| Week 1 | Indie Windows newsletters: **Pulse of the Microsoft Store** (substack), **Beautiful Pixels** (Windows app spotlight), **WindowsCentral**'s "indie app of the week" pitch | Editorial pickups drive durable installs, not just spikes. |
| Week 2 | ProductHunt launch (Tuesday) | Submit with a 30s screen-recording GIF of the bar filling. Pre-line up 5-10 hunters from your network for early upvotes. |
| Week 3 | Targeted DM to ~10 Windows productivity YouTubers (~50k subs each). Offer 2 codes each (one for them, one to give away). | Niche YouTubers convert better than big ones for utility tools. |
| Week 4 | Hacker News Show HN | Sundays underperform; aim for Tue/Wed 8am ET. Include the GitHub link + the Store link. Have the price-point story ready ("$4.99 one-time, no subscription"). |

### Code-tracking sheet

Maintain a small spreadsheet (or Linear ticket) so codes don't double-spend:

| Code (last 4) | Sent to | Date | Redeemed? | Review? |
|---|---|---|---|---|

Codes Microsoft sees as "unredeemed after 4 weeks" expire automatically.

---

## Post-launch metrics worth tracking

Partner Center → Acquisitions / Health gives you:

- **Daily installs** (free downloads of base app).
- **Conversion to Pro** — IAP purchases as a % of installs.
- **Refund rate** — should stay < 5% for a $5 utility. If it spikes, look at the most-refunded customers' review patterns (gradient bug? install failure?).
- **Crash-free sessions** — Partner Center reports this from Windows Telemetry. < 99.5% means there's a real crash to chase.

Gumroad's dashboard gives you direct-channel revenue + refunds separately. Cross-reference both: if direct refunds spike, the license-key UX is at fault; if Store refunds spike, the IAP/purchase UX is.

---

## Open follow-ups before flipping the launch switch

- [ ] Change `LicenseKey.DefaultSecret` from the shipped placeholder.
- [ ] Stand up the Gumroad webhook + key issuer.
- [ ] Buy a real direct-channel key end-to-end on a clean Windows VM, paste it into the upgrade dialog, verify the Pro chips clear.
- [ ] Generate 50 promo codes in Partner Center; populate the tracking sheet.
- [ ] Draft the launch posts (subreddit, PH, HN) — write them **before** launch day, schedule the rest.
