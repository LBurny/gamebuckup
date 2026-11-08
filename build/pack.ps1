param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$PlayniteDir = "D:\Playnite\Playnite"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$toolbox = Join-Path $PlayniteDir "Toolbox.exe"

if (-not (Test-Path $toolbox)) {
    throw "Playnite Toolbox not found at $toolbox"
}

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration

$output = Join-Path $root "src\GameBackup.Plugin\bin\$Configuration\net462"
$stage = Join-Path $root "dist\stage\GameBackup"
$dist = Join-Path $root "dist"

if (Test-Path (Split-Path -Parent $stage)) {
    Remove-Item (Split-Path -Parent $stage) -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null

Copy-Item (Join-Path $output "GameBackup.Plugin.dll") $stage
Copy-Item (Join-Path $output "GameBackup.Core.dll") $stage
Copy-Item (Join-Path $root "src\GameBackup.Plugin\extension.yaml") $stage
Copy-Item (Join-Path $root "src\GameBackup.Plugin\icon.png") $stage

& $toolbox pack $stage $dist

if ($LASTEXITCODE -ne 0) {
    throw "Toolbox pack failed with exit code $LASTEXITCODE"
}

Write-Host "Package written to $dist"
