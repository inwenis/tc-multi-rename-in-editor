param(
    [string]$Configuration = "Release"
)

# Builds TcBatchRename as a single-file, framework-dependent executable.
# Output: artifacts\publish\win-x64\TcBatchRename.exe
# Requires a matching .NET 10 desktop runtime on the target machine.
#
# Notes:
# - Uses the MSBuild property form (-p:SelfContained=false) rather than the
#   `--self-contained` CLI flag. With a RuntimeIdentifier set, that flag is not
#   reliably honored (it gets overridden back to self-contained on this SDK),
#   which silently produced a ~110 MB self-contained exe instead of the intended
#   framework-dependent one. The property form is respected.

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "TcBatchRename.csproj"
$outputPath = Join-Path $PSScriptRoot "artifacts\publish\win-x64"

dotnet publish $projectPath `
    -c $Configuration `
    -o $outputPath `
    -r win-x64 `
    -p:SelfContained=false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:RestoreIgnoreFailedSources=true `
    -p:NuGetAudit=false
