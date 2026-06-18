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

# Clean first so a changed ApplicationIcon (.ico) is always re-embedded into the
# EXE. MSBuild's incremental build does not treat the icon file as an input to its
# up-to-date check, so a stale icon survives plain rebuilds otherwise.
dotnet clean $proj -c $Configuration -r $Runtime | Out-Null

dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=embedded `
    -o $OutDir

Write-Host "Published to $OutDir"
