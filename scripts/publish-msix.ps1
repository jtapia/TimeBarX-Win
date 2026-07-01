[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\msix'),
    [string]$Manifest = (Join-Path $PSScriptRoot '..\Package.appxmanifest')
)

# Builds the Store-targeted MSIX package.
#
# Prerequisites (Windows-only):
#   - .NET 10 SDK
#   - Windows 10/11 SDK (provides makeappx.exe and signtool.exe)
#     Default install path: C:\Program Files (x86)\Windows Kits\10\bin\10.0.*\x64\
#   - You've reserved the app name in Partner Center and copied the Identity +
#     Publisher values into Package.appxmanifest (replace the PLACEHOLDER_* lines).
#
# What this script does NOT do:
#   - Sign the package. Partner Center accepts unsigned .msix and signs it for you
#     during certification. If you need a locally-signed build (sideloading),
#     run signtool yourself against the produced .msix; see STORE_SUBMISSION.md.
#
# Direct (Inno) builds are not affected — use scripts/publish.ps1 for those.

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $root 'src/TimeBarX.App/TimeBarX.App.csproj'

Write-Host "Building self-contained Windows publish for MSIX ($Configuration, $Runtime)..."

$layout = Join-Path $OutDir 'layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layout | Out-Null

# Force a clean obj/bin so a changed manifest or icon re-embeds. Same reasoning
# as scripts/publish.ps1: MSBuild's incremental check doesn't track these.
$appDir = Split-Path $proj -Parent
foreach ($dir in @('obj', 'bin')) {
    $path = Join-Path $appDir $dir
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

# Plain build first to avoid the single-file bundler race on a cold obj/.
dotnet build $proj -c $Configuration -r $Runtime --self-contained true `
    -f net10.0-windows10.0.19041.0 | Out-Null

dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -f net10.0-windows10.0.19041.0 `
    -p:PublishSingleFile=false `
    -p:DebugType=embedded `
    -o $layout

# Third-party native NuGets (SkiaSharp, HarfBuzzSharp) ship their .pdb debug
# symbols alongside their .dlls, and `dotnet publish` copies them into the
# output. They're not needed at runtime and add ~100MB uncompressed to the
# package (SkiaSharp alone is 84MB). Strip them before packing.
$pdbs = Get-ChildItem $layout -Recurse -Filter '*.pdb' -ErrorAction SilentlyContinue
if ($pdbs) {
    $totalMb = [Math]::Round(($pdbs | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Host "Stripping $($pdbs.Count) .pdb file(s), ~${totalMb} MB total, from the MSIX layout"
    $pdbs | Remove-Item -Force
}

# Drop manifest + Store tiles into the layout. The manifest's relative paths
# (assets\store-tiles\*.png) resolve from the package root, so we mirror that.
Copy-Item -Path $Manifest -Destination (Join-Path $layout 'AppxManifest.xml') -Force
$layoutAssets = Join-Path $layout 'assets\store-tiles'
New-Item -ItemType Directory -Force -Path $layoutAssets | Out-Null
Copy-Item -Path (Join-Path $root 'assets/store-tiles/*.png') -Destination $layoutAssets -Force

# Locate the newest installed Windows SDK that actually ships makeappx.exe.
# Old SDKs (10.0.14393.0 and earlier) don't include it; a plain string sort of
# directory names picks the wrong SDK when both old and new are installed
# (e.g. "10.0.9200.0" > "10.0.22621.0" alphabetically). Filter by presence of
# makeappx.exe first, then sort by parsed version and take the max.
$sdkParent = 'C:\Program Files (x86)\Windows Kits\10\bin'
if (-not (Test-Path $sdkParent)) {
    throw "Windows SDK not installed. Install a modern SDK (10.0.22621 or newer) via 'winget install Microsoft.WindowsSDK.10.0.22621' or the SDK installer at https://developer.microsoft.com/windows/downloads/windows-sdk/."
}
$kit = Get-ChildItem $sdkParent -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } |
    Sort-Object { [Version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $kit) {
    throw "Found Windows SDK(s) under $sdkParent, but none contain x64\makeappx.exe. Install SDK 10.0.22621 or newer (older SDKs don't ship makeappx)."
}
$makeappx = Join-Path $kit.FullName 'x64\makeappx.exe'
Write-Host "Using SDK $($kit.Name) for makeappx.exe"

$msix = Join-Path $OutDir 'TimeBarX.msix'
if (Test-Path $msix) { Remove-Item $msix -Force }

Write-Host "Packing MSIX → $msix"
& $makeappx pack /d $layout /p $msix /o

if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with exit code $LASTEXITCODE" }

Write-Host "Done. Upload $msix to Partner Center → Submission → Packages."
