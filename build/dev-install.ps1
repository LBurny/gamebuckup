param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration
}

$source = Join-Path $root "src\GameBackup.Plugin\bin\$Configuration\net462"
$target = Join-Path $env:APPDATA "Playnite\Extensions\GameBackup"

if (-not (Test-Path (Join-Path $source "GameBackup.Plugin.dll"))) {
    throw "Build output not found in $source"
}

if (Test-Path $target) {
    Remove-Item $target -Recurse -Force
}
New-Item -ItemType Directory -Path $target | Out-Null

# Playnite already ships the netstandard facades in its application directory.
# Copy only our own assemblies to avoid version conflicts.
Copy-Item (Join-Path $source "GameBackup.Plugin.dll") $target
Copy-Item (Join-Path $source "GameBackup.Core.dll") $target
Copy-Item (Join-Path $root "src\GameBackup.Plugin\extension.yaml") $target
Copy-Item (Join-Path $root "src\GameBackup.Plugin\icon.png") $target

Write-Host ""
Write-Host "Installed GameBackup to:"
Write-Host "  $target"
Write-Host "Restart Playnite to load the new build."
