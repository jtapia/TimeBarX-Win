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

# Drop manifest + Store tiles into the layout. The manifest's relative paths
# (assets\store-tiles\*.png) resolve from the package root, so we mirror that.
Copy-Item -Path $Manifest -Destination (Join-Path $layout 'AppxManifest.xml') -Force
$layoutAssets = Join-Path $layout 'assets\store-tiles'
New-Item -ItemType Directory -Force -Path $layoutAssets | Out-Null
Copy-Item -Path (Join-Path $root 'assets/store-tiles/*.png') -Destination $layoutAssets -Force

# Locate makeappx.exe in the latest installed Windows SDK.
$kit = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.*\x64' -ErrorAction Stop |
    Sort-Object Name -Descending | Select-Object -First 1
$makeappx = Join-Path $kit.FullName 'makeappx.exe'
if (-not (Test-Path $makeappx)) { throw "makeappx.exe not found under $($kit.FullName). Install the Windows 10/11 SDK." }

$msix = Join-Path $OutDir 'TimeBarX.msix'
if (Test-Path $msix) { Remove-Item $msix -Force }

Write-Host "Packing MSIX → $msix"
& $makeappx pack /d $layout /p $msix /o

if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with exit code $LASTEXITCODE" }

Write-Host "Done. Upload $msix to Partner Center → Submission → Packages."
