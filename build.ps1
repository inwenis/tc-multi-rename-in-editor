param(
    [string]$Configuration = "Release"
)

# Builds TcBatchRename as a single-file, framework-dependent executable.
# Output: artifacts\publish\win-x64\TcBatchRename.exe
# Requires a matching .NET 10 desktop runtime on the target machine.
# This is a thin wrapper over publish.ps1 so the dotnet publish logic lives in one place.

$ErrorActionPreference = "Stop"

$publishScript = Join-Path $PSScriptRoot "publish.ps1"

& $publishScript -Configuration $Configuration -Runtime win-x64 -SingleFile
