[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\publish')
)

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $root 'src/TimeBarX.App/TimeBarX.App.csproj'

Write-Host "Publishing TimeBarX ($Configuration, $Runtime)..."

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
dotnet build $proj -c $Configuration -r $Runtime --self-contained true

dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -o $OutDir

Write-Host "Published to $OutDir"
