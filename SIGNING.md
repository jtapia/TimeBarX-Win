# Code Signing — TimeBarX for Windows

Both the EXE and the installer must be signed before public distribution to
avoid SmartScreen "Unknown publisher" warnings.

## Prerequisites

- An EV or OV code-signing certificate, exported as `.pfx` (or accessible
  via certificate store / HSM).
- `signtool.exe` from the Windows SDK on PATH.

The `.pfx` file and its password live outside this repo. Two common stores:

- CI: a protected secret (GitHub Actions `secrets.WINDOWS_CERT_PFX_BASE64` +
  `WINDOWS_CERT_PASSWORD`). The job materializes the file at the start and
  shreds it at the end.
- Local: `%USERPROFILE%\.timebarx\signing.pfx`, never committed.

## Sign the EXE

```pwsh
$pfx = "$env:USERPROFILE\.timebarx\signing.pfx"
$pwd = Read-Host -AsSecureString "PFX password"

signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f $pfx /p (ConvertFrom-SecureString -SecureString $pwd -AsPlainText) `
    artifacts\publish\TimeBarX.App.exe
```

## Sign the installer

```pwsh
iscc scripts\installer.iss

signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f $pfx /p (ConvertFrom-SecureString -SecureString $pwd -AsPlainText) `
    artifacts\installer\TimeBarX-0.1.0-Setup.exe
```

## Verify

```pwsh
signtool verify /pa /v artifacts\installer\TimeBarX-0.1.0-Setup.exe
```

The output must end with `Successfully verified` and show a timestamped
signature chaining to a trusted root.

## Notes

- Sign the EXE BEFORE compiling the installer, so the installer ships a
  signed inner EXE.
- Always pass `/tr` (timestamping). Without a timestamp, the signature
  expires when the certificate does.
- Never commit `.pfx`, `.pvk`, certificate passwords, or signed binaries
  with embedded private keys.
