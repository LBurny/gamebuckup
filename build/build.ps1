param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = "D:\Code\dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    throw "dotnet SDK not found at $dotnet"
}

$env:DOTNET_ROOT = "D:\Code\dotnet"
& $dotnet build (Join-Path $root "GameBackup.sln") -c $Configuration

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}
