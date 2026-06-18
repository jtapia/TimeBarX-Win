# Releasing TimeBarX

Run these steps from a Windows host with the .NET 10 SDK, Inno Setup 6, and
the signing certificate available (see `SIGNING.md`). Bash and PowerShell
both work; the commands below use PowerShell.

---

## 1. Pre-flight

- All work for the milestone is merged to `main`.
- `dotnet build` and `dotnet test` are green.
- `CHANGELOG.md` Unreleased section reflects what's shipping.
- `<Version>` in `src/TimeBarX.App/TimeBarX.App.csproj` matches the planned
  tag (no `v` prefix in the csproj).
- `assets/icon.ico` exists (regenerate from `icon.svg` if missing —
  see `assets/README.md`).

---

## 2. Publish the self-contained build

```pwsh
pwsh scripts/publish.ps1
```

Output lands in `artifacts/publish/`. The EXE is a single file with the
.NET runtime embedded.

---

## 3. Sign the EXE

See `SIGNING.md` for the full incantation. Summary:

```pwsh
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f $env:USERPROFILE\.timebarx\signing.pfx /p $pwd `
    artifacts\publish\TimeBarX.App.exe
```

Sign the EXE before building the installer so the installer ships a signed
inner binary.

---

## 4. Build the installer

```pwsh
iscc scripts\installer.iss
```

Output: `artifacts/installer/TimeBarX-<version>-Setup.exe`.

---

## 5. Sign the installer

```pwsh
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f $env:USERPROFILE\.timebarx\signing.pfx /p $pwd `
    artifacts\installer\TimeBarX-<version>-Setup.exe

signtool verify /pa /v artifacts\installer\TimeBarX-<version>-Setup.exe
```

`signtool verify` must end with `Successfully verified`.

---

## 6. Smoke test

On a clean Windows VM (no prior install, no dev certs trusted):

- Run the installer. Confirm no SmartScreen warning beyond the certificate
  prompt for a fresh signing identity.
- Launch TimeBarX, start a 1-minute timer from the tray, confirm overlay
  paints on every monitor.
- Press Ctrl+Shift+T, enter `30s test`, confirm tooltip shows the label
  and the completion flash/pulse/fade plays.
- From Run dialog: `timebarx://start?duration=20s` — confirm the existing
  instance receives the URI (no second tray icon).
- Reboot the VM; confirm the in-flight timer rehydrates.
- Uninstall; confirm Start Menu shortcut, registry URI handler, and Run-on-
  startup entry are all removed.

---

## 7. Tag and publish

```pwsh
git tag vX.Y.Z
git push --tags
```

Create the GitHub release from the tag. Attach the signed installer and
paste the matching `CHANGELOG.md` section as the release body.

---

## 8. Post-release

- Move `CHANGELOG.md`'s shipped entries from `Unreleased` to the new
  version heading.
- If `TIMEBARX_UPDATE_URL` is in use, update the hosted `versions.json` so
  existing installs pick up the new version on next launch.
