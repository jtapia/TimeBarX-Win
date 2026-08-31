# Microsoft Store Pricing — Analysis & Recommendation

> Research date: **June 2026**. All prices in **USD**. Where a price could not be
> confirmed on a live listing, it is marked _(unverified)_ — Microsoft Store
> listing pages are JS-rendered and don't expose exact prices to scrapers, so
> some figures are drawn from reviews/dev sites and should be re-checked on the
> live page before quoting.

## TL;DR recommendation

**Ship free with an optional one-time "Pro" unlock (freemium-lite), not paid-upfront.**

- **Microsoft Store:** Free download. Core timer fully usable for free. A single
  one-time **$4.99 "Pro" in-app purchase** unlocks the power-user surface
  (gradient/custom colors, "Always above everything", URI automation, multi-preset
  customization, future themes).
- **Direct (Inno Setup installer, outside the Store):** offer the same Pro unlock
  via a license key sold through Gumroad/Paddle, or simply **"pay what you want /
  one-time $5"**. Direct sales avoid even the 15% Store cut.
- **Avoid paid-upfront.** In this category it crushes install volume, and install
  volume is what a free utility converts on.

Rationale below.

---

## 1. What TimeBarX is (positioning)

A lightweight, single-purpose **edge-of-screen progress-bar timer**: thin
always-visible bar pinned to the screen edge, click-through, multi-monitor,
global hotkey, natural-language duration input, `timebarx://` URI automation,
optional taskbar-fill mode. The Windows counterpart to a macOS menu-bar timer.

This is **not** a full Pomodoro/task-management suite (no task lists, no
analytics dashboards, no cloud sync). That matters for pricing: it competes on
*elegance and unobtrusiveness*, not feature breadth — so it can't justify a
subscription, but it can justify a small one-time unlock.

---

## 2. Competitor landscape

### Microsoft Store — Pomodoro / focus / countdown timers

The segment is **dominated by free and freemium apps**, with a thin layer of
one-time "PRO" upsells. Observed listings (June 2026):

| App | Model | Notes |
|-----|-------|-------|
| Pomodoro Timer (multiple same-named apps) | Free | Basic 25/5 timers, ad-supported or no monetization |
| Focus To-Do | **Freemium** | Free Pomodoro + tasks; Premium unlock (cross-device sync, reports) via IAP/subscription |
| Focus Commit: Pomodoro & Task Planner | Freemium | Free core; paid premium tier |
| PowerPom | Free | Simple Pomodoro |
| Focus Journal – Advanced Pomodoro Timer | Free | Pomodoro + journaling |
| Pomo Focus | Freemium-ish | Free; Discord integration, gamified "garden" rewards |
| Pomodoro Pro | Free/Freemium | Time-blocking, floating mini-HUD, task tracking |
| **Pomodoro Timer PRO – Focus & Productivity** | **One-time purchase** _(price unverified)_ | Explicitly "one-time payment, no subscriptions"; unlimited categories, advanced stats, Stream Deck, web dashboard |

**Pattern:** Free download is the default. Monetization, when present, is a
freemium unlock (IAP or subscription) layered on a usable free core. Pure
paid-upfront is rare and is the exception, not the norm. A "PRO, one-time, no
subscription" framing is used as a *differentiator* against the subscription
crowd.

### Broader market — macOS / cross-platform lightweight focus timers

Informs the price band even though it's a different store:

| App | Platform | Model | Price |
|-----|----------|-------|-------|
| **Be Focused Pro** (xwavesoft) | macOS / iOS | One-time | **macOS $4.99**, iOS $1.99 _(historical; verify live)_ |
| Flow (flowapp.info) | macOS / iOS | Freemium subscription | Free tier + Pro from **~$1.49/mo** (annual) |
| Session | macOS / iOS | Subscription | **~$5/mo** _(reported)_ — full-featured (sync, analytics, blocking) |
| Focus (Meaningful Things) | macOS / iOS | Subscription | **$39.99/yr**, 7-day trial |
| Focused Work | macOS / iOS | Subscription + lifetime | $4.99/mo or $29.99/yr, **$59.99 lifetime** |
| Forest | iOS/Android/web | Paid + IAP | Mobile paid app + coin IAP |

**Pattern:** Two tiers exist. **Heavy, sync-and-analytics apps** charge
subscriptions ($1.49–$5/mo, $30–$40/yr). **Lightweight menu-bar/edge timers**
that do one thing well are **one-time, sub-$10** — typically **$4.99**
("two cups of coffee," as reviewers frame it). TimeBarX squarely belongs in the
second group.

---

## 3. Microsoft Store economics (2026)

Verified from Microsoft/news sources (May–Sep 2025 changes, current in 2026):

- **Revenue share:**
  - Use **your own commerce** for a non-gaming app → **keep 100%** (0% Microsoft cut).
  - Use **Microsoft's commerce** (IAP/subscriptions/one-time) → **15% for apps**
    (12% for games). This is far below Apple/Google's historic 30%.
- **Developer account:** Publishing fees **waived** — individual *and* company
  accounts are now **free** (previously ~$19 individual / ~$99 company).
- **Pricing options:** One-time price, free, free trial, in-app purchases, and
  subscriptions are all supported. Free time-limited trials surface as
  "Free trial" on the listing.
- **Reach:** Microsoft Store cited at **250M+ monthly active users** — meaningful
  organic discovery for a free app, negligible for a paid-upfront niche utility.

**Implication:** The Store is cheap to ship on and the 15% (or 0% with own
commerce) cut means a one-time IAP nets nearly the full price. There is no
financial reason to gate the whole app behind a paywall.

---

## 4. Why freemium-lite beats the alternatives

**Paid-upfront ($X to even download):**
- ❌ In a free-dominated category, a price tag collapses install volume. With
  250M MAU, free + IAP exposes the app to organic discovery that a paid listing
  never gets.
- ❌ No try-before-buy friction-free path; reviews/word-of-mouth start from a
  tiny base.
- ✅ Only works if the app is already known/recommended. TimeBarX isn't yet.

**Subscription:**
- ❌ Unjustifiable for a no-backend, no-sync, no-server local utility. Users
  revolt at "rent" for a timer. The macOS comps that subscribe all ship sync +
  analytics + blocking infrastructure TimeBarX deliberately doesn't have.

**Freemium-lite (free core + one-time Pro unlock) — recommended:**
- ✅ Matches the dominant Store pattern (free download) *and* the
  lightweight-timer price norm (one-time, ~$5).
- ✅ Maximizes installs → reviews → ranking → more installs.
- ✅ The "one-time, no subscription" framing is a genuine differentiator and a
  recurring selling point reviewers love.
- ✅ 15% cut (or 0% direct) means a $4.99 unlock nets ~$4.24 (Store) or ~$4.99
  (direct, minus payment processor).

---

## 5. Recommended free vs. Pro split

Keep the free tier genuinely useful (it's the marketing), gate the
power-user/cosmetic surface.

**Free (the hook — never cripple the core timer):**
- Start/pause/resume/stop, all preset durations, natural-language input
- The bar itself: top **or bottom / taskbar-fill** position, multi-monitor, click-through, completion effects
- A default color + the standard height options
- Global hotkey, persistence across restart/sleep

**Pro — one-time $4.99 unlock:**
- Custom/gradient colors + future theme packs
- **"Always above everything"** experimental mode
- `timebarx://` **URI automation** + integrations (PowerToys/Flow/AutoHotkey)
- Per-preset customization / saved custom presets
- (Optional) opacity fine-tuning

> _Revised after launch decision: bottom/taskbar-fill position moved Free → it's a core placement choice, not a power-user extra._

> Tune the line so the free tier is "delightful and complete for casual use" and
> Pro is "for people who automate and customize." Avoid putting anything that
> makes the bar *work at all* behind the wall.

### Price point

- **$4.99 one-time** is the sweet spot — matches Be Focused Pro and the
  category's "two coffees" anchor; low enough to be an impulse unlock.
- Avoid $9.99+ (reads as expensive for a single-purpose utility).
- Avoid $0.99 (signals throwaway; leaves money on the table for a polished tool).
- Consider a brief launch promo (free Pro for early reviewers) to seed ratings.

---

## 6. Direct (non-Store) channel

The app also ships as an Inno Setup installer outside the Store. For direct sales:

- Sell the same Pro unlock as a **license key** via **Gumroad** (~10% + fees) or
  **Paddle** (~5% + fees, acts as merchant of record / handles VAT).
- Or **pay-what-you-want with a $5 suggested** price for goodwill + GitHub-style
  distribution.
- Direct avoids the Store's 15%, but the Store's discovery (250M MAU) usually
  outweighs the fee for a new app — treat direct as the channel for power users
  who found you via GitHub/README, not the primary funnel.

---

## 7. Open follow-ups before publishing

- [ ] Re-verify **Pomodoro Timer PRO** and **Be Focused Pro** live prices on
      their store pages (figures here partly from reviews/dev sites).
- [ ] Decide whether `timebarx://` automation should be free (it's a power-user
      hook that could *drive* adoption) or Pro (it's a clear "advanced" feature).
      Leaning Pro, but it's the most debatable line item.
- [ ] Confirm Microsoft Store supports the one-time **IAP unlock** flow you want
      (vs. a separate paid SKU) in Partner Center.
- [ ] Decide free-vs-Pro for **bottom/taskbar-fill** — it's a marquee visual that
      might be better as a free hook to show the app off in screenshots.

---

## Sources

- [Pomodoro/focus timers — Microsoft Store search (apps.microsoft.com)](https://apps.microsoft.com/detail/9n9w8c7pxwzd?hl=en-US&gl=US)
- [Focus To-Do — Microsoft Store](https://apps.microsoft.com/detail/9n8gpb2tk8gb?hl=en-US&gl=US)
- [Microsoft makes company developer accounts free — Neowin](https://www.neowin.net/news/microsoft-makes-company-developer-accounts-free-for-the-microsoft-store/)
- [It'll soon be free to publish apps to the Microsoft Store — TechCrunch](https://techcrunch.com/2025/05/19/itll-soon-be-free-to-publish-apps-to-the-microsoft-store/)
- [Microsoft drops all Windows Store fees — TechBuzz](https://www.techbuzz.ai/articles/microsoft-drops-all-windows-store-fees-undercutting-apple-google)
- [Get started with Microsoft Store — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/publish/get-started)
- [Be Focused Pro — Mac App Store](https://apps.apple.com/us/app/be-focused-pro-focus-timer/id961632517?mt=12)
- [7 Best Mac Focus Timer & Pomodoro Apps in 2026 — seam Blog](https://getseam.app/blog/best-mac-focus-timer-pomodoro-apps)
- [The 6 best Pomodoro timer apps — Zapier](https://zapier.com/blog/best-pomodoro-apps/)
- [Be Focused Pro Review 2025 — productivity.directory](https://productivity.directory/be-focused-pro)
