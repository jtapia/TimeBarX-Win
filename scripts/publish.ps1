[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    # The App project multi-targets (net10.0 + net10.0-windows…). `dotnet publish`
    # requires a single framework (NETSDK1129), so pin one. The direct/Inno
    # channel uses the Windows TFM so the real StoreEntitlements purchase channel
    # is compiled in (the plain net10.0 TFM would fall back to MockEntitlements,
    # which grants Pro from a TIMEBARX_PRO env var — not something to ship).
    [string]$Framework = 'net10.0-windows10.0.19041.0',
    # Default to arch-specific publish dir so x64 and arm64 don't clobber each
    # other. scripts/installer.iss reads from ..\artifacts\publish-<Runtime>\
    # by the same convention (see the Arch flag in installer.iss).
    [string]$OutDir = (Join-Path $PSScriptRoot "..\artifacts\publish-$Runtime")
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $root 'src/TimeBarX.App/TimeBarX.App.csproj'

Write-Host "Publishing TimeBarX ($Configuration, $Runtime, $Framework)..."

# Clear build output so a changed ApplicationIcon (.ico) is always re-embedded
# into the EXE. MSBuild's incremental build does not treat the icon file as an
# input to its up-to-date check, so a stale icon survives plain rebuilds. We
# delete obj/bin directly rather than `dotnet clean -r`, which fails when the
# RID hasn't been restored yet (NETSDK1047).
$appDir = Split-Path $proj -Parent
foreach ($dir in @('obj', 'bin')) {
    $path = Join-Path $appDir $dir
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

# Warm up the RID restore + a plain (non-single-file) build first. Publishing a
# single file straight after wiping obj/bin can race in the bundler step
# ("forbidden to change Manifest state after it was written"); a prior build
# populates the intermediate state so the bundle step runs cleanly.
dotnet build $proj -c $Configuration -r $Runtime -f $Framework --self-contained true

dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    -f $Framework `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -o $OutDir

Write-Host "Published to $OutDir"
