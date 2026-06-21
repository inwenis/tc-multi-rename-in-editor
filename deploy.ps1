param(
    [string]$Target = "C:\programki",
    [string]$Configuration = "Release"
)

# Builds TcBatchRename, then deploys it to $Target on this machine.
#
# - Always rebuilds first so the deployed exe matches current source.
# - Creates $Target if it does not exist.
# - Copies TcBatchRename.exe (overwrites).
# - Copies tc-batch-rename.json only if it is missing at $Target, so a tuned
#   deployed config is preserved across redeploys. The first deploy seeds the
#   committed config; if none ships, the helper creates a default on first run.

$ErrorActionPreference = "Stop"

$buildScript = Join-Path $PSScriptRoot "build.ps1"
& $buildScript -Configuration $Configuration

$publishDir = Join-Path $PSScriptRoot "artifacts\publish\win-x64"
$exeSource = Join-Path $publishDir "TcBatchRename.exe"

if (-not (Test-Path $exeSource)) {
    throw "Build did not produce $exeSource"
}

if (-not (Test-Path $Target)) {
    New-Item -ItemType Directory -Path $Target | Out-Null
    Write-Host "Created $Target"
}

$exeTarget = Join-Path $Target "TcBatchRename.exe"
Copy-Item -Path $exeSource -Destination $exeTarget -Force
Write-Host "Deployed $exeTarget"

$configSource = Join-Path $publishDir "tc-batch-rename.json"
$configTarget = Join-Path $Target "tc-batch-rename.json"
if ((Test-Path $configSource) -and -not (Test-Path $configTarget)) {
    Copy-Item -Path $configSource -Destination $configTarget
    Write-Host "Seeded $configTarget"
}
elseif (Test-Path $configTarget) {
    Write-Host "Kept existing $configTarget"
}
