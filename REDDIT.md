# TimeBarX — Reddit promo playbook

Scripts for promoting the Windows edge-of-screen progress-bar timer. Each
post is tuned to the sub's culture — don't cross-post verbatim, and don't
open with the download link. Read each sub's rules before you post; some
require flair, some ban self-promo outright unless you're a mod-approved
contributor.

## Ground truth (what the app actually does — don't overclaim)

Free tier:
- Thin progress bar pinned to the top or bottom edge of every monitor
- Click-through and always-visible; never steals focus
- Global hotkey **Ctrl + Shift + T** opens a quick-input window
- Natural-language duration parsing: `25m`, `1h 30m`, `1:30`, `2h review PR`, `half hour`, `quarter hour`
- Completion effects: flash → three pulses → 2 s fade (optional sound off by default)
- `timebarx://` URI scheme with `start` / `pause` / `resume` / `stop`
- Integrates with PowerToys Run, Flow Launcher, AutoHotkey, Windows Shortcuts
- Auto-hides during fullscreen VLC / mpv / Netflix / Disney+ / Hulu / MPC-HC
- Persists timer + settings across restart, sleep, and wake
- Blue bar, height/opacity/position controls, tray menu

Pro tier ($4.99 one-time via Microsoft Store add-on, or direct license key):
- Full color palette (Accent, Purple, Green, Red)
- Gradient mode
- "Always above everything" experimental mode (top-most reassertion + exclusive-fullscreen detection)
- Custom presets

Do NOT claim: cloud sync, teams, mobile companion, AI, analytics. It has
none of those on purpose.

---

## Post 1 — r/windows (main "show off my app" candidate)

**Title (pick one):**
- I built a progress-bar timer that lives on the edge of your screen — Windows, no window, no dock
- Made a Pomodoro-ish timer that renders as a thin bar across the top of every monitor
- Made a thin timer bar that pins to the top of every monitor, click-through — feedback welcome

**Body:**

> I got tired of Pomodoro timers stealing focus or hiding in a tray icon I forget about, so I built one that renders as a **thin progress bar pinned to the top edge of every monitor**. Click-through, always visible, never breaks focus.
>
> ```
> ██████████████████████░░░░░░░░░░░░░░
> ```
>
> Features (free tier):
> - `Ctrl+Shift+T` opens a quick-input window that parses `25m`, `1h 30m`, `1:30`, `2h review PR`, `half hour`, etc.
> - Auto-hides during fullscreen VLC / mpv / Netflix / Disney+ / Hulu so it doesn't sit over your movie
> - `timebarx://` URI scheme — plugs into PowerToys Run, Flow Launcher, AutoHotkey, or a plain desktop shortcut
> - State survives restart and sleep/wake
> - Built in .NET 10 + Avalonia; runs on Windows 10 1809+
>
> There's a $4.99 Pro tier for color/gradient/custom-presets/always-above-fullscreen — but everything above is free and unlimited. No account, no telemetry, no cloud, no ads.
>
> Trying to submit to the Microsoft Store now; happy to answer implementation questions (click-through overlays over the taskbar were a fight).

**Tone notes:** Windows sub tolerates polish + technical honesty, hates "revolutionary" language. Lead with the visual bar. Mention the technical fight (fullscreen Z-order, click-through) — it earns credibility.

---

## Post 2 — r/productivity (framing = focus tool, not devtool)

**Title (pick one):**
- Made a timer that shows progress as a bar across the top of your screen instead of a number
- I stopped installing Pomodoro apps because the popups broke my focus, so I made one that's just a bar at the edge of my screen
- A focus timer that hides in your peripheral vision instead of a popup — free, Windows
- Traded my tomato-shaped timer for a thin progress bar across the top of my monitor. Focus went up.

**Body:**

> Everything I tried for time-boxing either popped up a window every session (breaks flow) or hid in a tray icon I'd forget about (defeats the purpose). Built one that just... sits at the edge of every monitor as a **thin progress bar that drains as time runs out**.
>
> - Global hotkey `Ctrl+Shift+T` to start something: type `25m focus`, `1:30 deep work`, `half hour`, or `10m break`
> - Click-through, so it never gets in your way
> - Auto-hides for fullscreen video, so it doesn't sit over Netflix during your break
> - No account, no cloud, no notifications you have to dismiss
>
> Free version has everything I actually use; $4.99 one-time unlocks colors + a few power features. Not looking for revenue as much as feedback — is there a focus-tool feature I'm missing that a bar-shaped UI could actually solve?

**Tone notes:** This sub rewards honesty about the "why" and punishes marketing language. Ask a question at the end. Do NOT link in the title. Post on Sunday evening or Monday morning US-time.

---

## Post 3 — r/PomodoroTechnique / r/getdisciplined

**Title (pick one):**
- Made a Pomodoro timer for Windows that renders as a bar across the top of the screen
- Fixed my Pomodoro habit by making the timer bar *become* my monitor's top edge (no popup)
- Peripheral-vision Pomodoro timer for Windows — the bar drains, you keep working

**Body:**

> Pomodoro app fatigue is real — I kept installing tomato-shaped apps and then closing the popup and losing track. Built a version where the timer *is* the screen border: a thin progress bar draining across the top of every monitor. Peripheral vision does the reminding, no notifications needed.
>
> - `Ctrl+Shift+T` → `25m` → running
> - `Ctrl+Shift+T` → `5m break` → next cycle
> - Auto-hides during fullscreen video (so it doesn't stalk you on breaks)
> - Free tier is fully functional; Pro ($4.99 one-time) adds color/gradient/custom presets
>
> Windows only right now (there's a macOS sibling if anyone wants that). If you've tried it, I'd love to know what preset patterns you'd want built-in.

**Tone notes:** Community here is small but engaged. Personal anecdote > product pitch. Ask for input on presets — actually useful and generates comments.

---

## Post 4 — r/coolgithubprojects / r/csharp / r/dotnet (dev-audience)

**Title (pick one):**
- TimeBarX — edge-of-screen progress bar timer for Windows (.NET 10 + Avalonia, open questions on Win32 overlay tricks)
- Fighting Win32 to keep a click-through overlay above the taskbar: what I learned building a Windows timer in Avalonia
- Built a Windows tray timer in .NET + Avalonia — writeup on the click-through / top-most / fullscreen-detection dance
- Avalonia + Win32 interop for a click-through progress bar that survives the Windows 11 shell (mostly)

**⚠ Runtime version:** double-check the Avalonia + .NET version against your `.csproj` before posting. If you shipped against .NET 9 (not 10), the title/body must reflect that — r/csharp will call it out within minutes.

**Body:**

> Sharing a small Windows tray app I built with .NET 10 + Avalonia: a thin progress-bar timer pinned to the edge of every monitor. Click-through, always visible, click-through overlay via `WS_EX_TRANSPARENT | WS_EX_LAYERED` + `SetLayeredWindowAttributes(LWA_ALPHA)`.
>
> A few Win32 things that took real fighting:
> - Persistent top-most over the Windows 11 shell is a losing game when the Start menu / StartMenuExperienceHost is invoked. Ended up conceding "work-area placement" as the default and reserving "always above everything" for an opt-in Pro mode that also runs an exclusive-fullscreen-detection loop.
> - Click-through with a rendered progress bar had to keep `WS_EX_LAYERED` set — dropping it broke the DWM-composed path and the bar wouldn't render at all.
> - Single-instance URI forwarding via a named pipe so `timebarx://start?duration=25m` from PowerToys Run always hits the running tray.
>
> Architecture: `TimeBarX.Core` is platform-neutral (timer engine, JSON persistence, natural-language duration parser, `IEntitlements` abstraction); `TimeBarX.App` is the Avalonia + Win32 host. Tests: 112 xUnit, deterministic via a `FakeClock`.
>
> Freemium: free tier is fully functional; Pro ($4.99 via Store add-on or direct license key) unlocks color palette + gradient + custom presets. HMAC-SHA256 signed offline license keys for the direct channel; `Windows.Services.Store` for the MSIX build.
>
> Happy to answer questions on any of the above — Avalonia + Win32 interop, MSIX packaging, or the freemium plumbing.

**Tone notes:** Dev subs reward specificity. Name the Win32 flags, mention the constraints you conceded, credit the framework. This is the post most likely to hit /r/csharp front page — but only if the technical content is real. Don't inflate.

---

## Post 5 — r/SideProject

**Title (pick one):**
- I made a timer that renders as a bar across your monitor — no window, no popup
- Six-week side project: a Windows timer where the *bar itself* is the UI, no window
- Shipped my first Windows side project — a click-through progress-bar timer that lives on your monitor's edge
- Turned "I forget my timer is running" into "the timer *is* the top of my monitor" — feedback on the free tier?

**Note:** dropped r/InternetIsBeautiful from this slot — that sub is heavily curated for websites, not desktop apps, and product-only pitches get removed. r/software (below, Post 7) is the natural replacement.

**Body:**

> Six-week side project: a Windows timer that lives on the edge of your screen instead of in a window. Thin progress bar drains across the top of every monitor, click-through, doesn't interrupt anything.
>
> [screenshot]
>
> - Free tier: hotkey, natural-language input, URI scheme, integration with PowerToys/AutoHotkey/Flow Launcher, auto-hide for fullscreen video, persistent across restart/sleep
> - Pro ($4.99 once): color palette, gradient, custom presets, always-above-fullscreen
> - No account, no cloud, no telemetry
>
> On Microsoft Store approval right now; would appreciate feedback on the free tier before it hits general release.

**Tone notes:** r/SideProject is friendly to plugs but demands a screenshot or GIF. Lead with the GIF; the body is second. Reply to every comment in the first 3 hours.

---

## Post 6 — r/PowerToys (integration angle — highest-signal, lowest-risk sub for this app)

**Why this post exists:** the `timebarx://` scheme + PowerToys Run alias is the single most differentiating feature and this sub's audience *is* the people who wire PowerToys into their workflow. Frame the post as "here's a PowerToys Run recipe I built for myself," not "here's my app."

**Title (pick one):**
- PowerToys Run alias I wired up to launch timers on my monitor's top edge
- Set up a PowerToys Run alias for a click-through progress-bar timer — sharing the recipe
- `>25m` in PowerToys Run → thin timer bar draining across every monitor. Wanted to share how I built it.

**Body:**

> If you already run PowerToys Run for `calc`, `search`, and app launching, you can also point it at a lightweight tray timer via a URI-scheme alias. I built the tray app (open to feedback), but the workflow works with any URI-registering timer.
>
> **The recipe:**
> 1. Register a URI handler on Windows for `timebarx://start?duration={query}`
> 2. In PowerToys Run's URI Handler plugin, add `>` as a prefix
> 3. `>25m focus` in Run → a thin progress bar drains across the top of every monitor for 25 minutes
>
> No popup, no window steal, no tray dive. Click-through so it never gets in the way.
>
> The tray app is [TimeBarX](https://…) — free tier is fully functional (natural-language input, auto-hide during fullscreen VLC/Netflix, persistent across restart/sleep), $4.99 Pro adds color/gradient/custom presets. Also plugs into Flow Launcher and AutoHotkey via the same URI scheme.
>
> Would love to hear other PowerToys Run integrations people have set up — I have three or four aliases now and they've compounded into a real launcher habit.

**Tone notes:** r/PowerToys audience is technical, PowerToys-native, and hates "check out my app" framing. Frame it as "sharing a recipe I built for myself, here's the tool that makes it work." Asking about *their* PowerToys aliases at the end is genuine — you'll learn integration ideas.

---

## Post 7 — r/Windows11 / r/software (broader utility discovery)

**Why this post exists:** r/Windows11 is more active than r/windows for utility discovery in 2026; r/software is small but a real recommendation engine. Same body works for both — post 5+ days apart, different titles.

**Title (pick one):**
- Made a Windows 11 utility: thin timer bar that pins to the top edge of every monitor (click-through)
- Timer for Windows 11 that renders as a bar across your monitor's edge — never covers what you're doing
- A Windows utility I've been using every day: edge-of-screen progress bar timer, click-through, auto-hides for video

**Body:**

> Windows 11 utility I built and have been using every day for six weeks: a **thin progress bar pinned to the top edge of every monitor** that drains as your timer runs. Click-through and always visible — you can see it, but it never intercepts a click.
>
> - `Ctrl+Shift+T` opens a quick input. Type `25m`, `1h 30m`, `1:30`, `2h review PR`, `half hour` — it parses all of them
> - `timebarx://` URI scheme plugs into PowerToys Run, Flow Launcher, AutoHotkey, or a plain Windows shortcut
> - Auto-hides during fullscreen VLC / mpv / Netflix / Disney+ / Hulu — doesn't sit over your movie
> - Survives restart and sleep/wake
> - No account, no cloud, no telemetry, no notifications you have to dismiss
>
> Free tier is fully functional; $4.99 one-time Pro adds a color palette, gradient mode, custom presets, and an "always above everything" mode for full-screen games.
>
> On Microsoft Store now (or: in cert review — swap when live). Feedback on the free tier before it hits general release would be great.

**Tone notes:** r/Windows11 tolerates polished self-promo if you lead with the utility, not the price. r/software has a stricter "no marketing" tone — for that one, drop the last line and just say "sharing in case someone finds it useful."

---

## Self-promo disclosure — required on several subs

Reddit's site-wide 9:1 rule (nine non-promo contributions for every self-promo post) plus per-sub rules mean this can't be optional. Get it wrong once and the account is shadowbanned from the sub.

- **r/windows, r/Windows11:** allow self-promo but require the `Self-Promotion` flair. Add `[OC]` or `I made this` to the title if there's no flair option.
- **r/productivity:** self-promo allowed once/week, must be flaired `Software / Apps`, and must include a **first-comment disclosure** like *"Disclosure: I'm the dev. Free tier is unlimited, $4.99 Pro is optional. No account, no data collection."*
- **r/PomodoroTechnique, r/getdisciplined, r/SideProject:** relaxed — dev disclosure in the body is enough.
- **r/PowerToys:** no explicit rule; add "I built the tray app" inline early in the body. Frame as recipe-sharing, not product-launching.
- **r/csharp, r/dotnet, r/coolgithubprojects:** self-promo fine if the technical content is substantive. Dev-audience subs punish thin technical posts far more than they punish self-promo.
- **r/software:** allow one self-promo per author per week. No aggressive marketing language.

**First-comment template** (post immediately after the OP so it's the top reply):

> Disclosure: I'm the dev. Everything shown above is in the free tier, no strings. The $4.99 Pro tier is a one-time payment (no subscription), and the app runs the same whether you buy it or not — no nagging, no "upgrade" prompts every launch. Happy to answer anything about the build, the Win32 dance, or the design decisions.

---

## Predictable comments — have answers ready

Every product post gets these. Being caught flat-footed makes the OP look defensive; being ready wins the thread.

**"Why isn't this free / open-source?"**
> Fair question. The free tier isn't a demo — it's what I use daily. Pro exists so I can afford to keep maintaining it (Microsoft Store fees, code-signing cert, notarization all cost money). Open-sourcing is on my list, but I want the code to hit "I'm not embarrassed by it" before I put it public. Happy to answer specific implementation questions here in the meantime.

**"Why not just use \[Windows built-in timer / Focus Sessions / big-name app\]?"**
> Windows Focus Sessions is a great fit for people who want the full session UI; this is the opposite bet — no UI, no session concept, just a bar that drains where you already look. If Focus Sessions works for you, you don't need this.

**"Isn't this just a progress bar? I could build it in an afternoon."**
> Genuinely, yes — the *idea* is small. The friction is in the corners: click-through overlays that stay above the Win11 shell, exclusive-fullscreen detection that doesn't false-positive on browsers, single-instance URI forwarding, MSIX packaging, notarization. Happy to compare notes if you build one; the Win32 overlay stuff was the fight.

**"Does it phone home / collect data?"**
> No. No account, no telemetry, no analytics, no cloud sync. The app doesn't make any network calls except the license-check for direct-channel Pro (offline HMAC-signed key; validates without hitting a server after activation). MSIX build uses `Windows.Services.Store` for Store-purchased Pro.

---

## What to prepare before posting

- [ ] A 5-second GIF showing: quick-input window → bar starts draining → completion flash. This does 80% of the work.
- [ ] A single screenshot of the settings window (dark mode) for the still-image posts.
- [ ] Store listing URL (once submission clears certification).
- [ ] The `timebarx://` URI scheme demo (screenshot of a PowerToys Run alias) — the "you can wire this into your workflow" angle is the most differentiating.
- [ ] A one-paragraph "about the dev" for the comment where someone always asks. Keep it short — "solo dev, macOS sibling app, this is the Windows counterpart".

## Anti-patterns

- No "revolutionary" / "game-changer" / "the productivity tool you've been waiting for". Reddit smells it instantly.
- No leading with the price. Lead with the visual, then the workflow, then mention there's a paid tier.
- No cross-posting the same body verbatim across subs on the same day — spam filters catch it and mods dislike it.
- No begging for upvotes or "if this helped, please share". Ends the thread.
- No responding defensively to criticism. First negative comment: acknowledge, ask a follow-up, thank them. It reads well to lurkers.
- Do NOT run all five posts in one week. Space them: r/csharp Monday, r/windows Wednesday, r/SideProject Saturday, hold r/productivity + r/PomodoroTechnique until you have 3+ pieces of user feedback to reference.

## When to post (rough US-timezone bands)

- r/windows, r/Windows11, r/productivity: Sunday 6-9pm ET or Monday 8-11am ET.
- r/csharp, r/dotnet, r/coolgithubprojects: Tuesday–Thursday 9am-noon ET.
- r/PowerToys: Tuesday–Thursday 9am-noon ET (dev-heavy audience, same window as r/csharp).
- r/SideProject: Saturday morning US.
- r/software: weekday morning US, low traffic — timing barely matters.
- r/PomodoroTechnique: any weekday morning; small enough that timing barely matters.

Space posts so no two hit on the same day. A reasonable one-week sequence:
- **Mon** r/csharp (dev-audience, sets the technical credibility base)
- **Tue** r/PowerToys (integration recipe — different framing, no overlap)
- **Wed** r/windows *or* r/Windows11 (utility discovery)
- **Sat** r/SideProject (screenshot/GIF post)
- **Following week** r/productivity + r/PomodoroTechnique once you have 3+ pieces of user feedback to reference in comments.

## After a post lands

- Reply to every comment in the first 3 hours. Reddit ranks engagement velocity.
- If someone reports a bug in comments, fix it visibly ("shipped in v1.0.1, thanks u/x") — nothing generates upvotes like a public bugfix loop.
- Screenshot the top comment thread and drop it into `assets/` for the Store listing later.
