# TimeBarX Automation

TimeBarX accepts the `timebarx://` URI scheme. Any launcher, shortcut, or
script that can open a URL can drive the timer.

## URI scheme

```
timebarx://start?duration=25m
timebarx://start?duration=1:30
timebarx://start?duration=2h%20review%20PR
timebarx://start?duration=25m&label=focus
timebarx://pause
timebarx://resume
timebarx://stop
```

`duration` accepts the same forms as the quick-input window: `25m`,
`1h 30m`, `1:30`, `90s`, `half hour`, etc. Any trailing words inside the
duration are treated as the timer label, or pass `label=...` explicitly.

The app is single-instance: launching `TimeBarX.exe timebarx://...` while
the app is already running forwards the URI over a named pipe instead of
starting a second process.

## PowerToys Run

Create a Program shortcut at `%LOCALAPPDATA%\TimeBarX\start-25.lnk` whose
target is `TimeBarX.exe` with arguments `timebarx://start?duration=25m`.
PowerToys Run picks it up automatically; type "start 25" to fire.

## Flow Launcher

Install the *URL* plugin and add a custom command:

```
focus = timebarx://start?duration=25m&label=focus
break = timebarx://start?duration=5m&label=break
stop  = timebarx://stop
```

## AutoHotkey

```ahk
^!1::Run "timebarx://start?duration=25m"
^!2::Run "timebarx://start?duration=5m&label=break"
^!0::Run "timebarx://stop"
```

## Windows Shortcuts

Right-click on the desktop → New → Shortcut → enter `timebarx://start?duration=25m`.
The shortcut becomes a one-click Pomodoro starter.
