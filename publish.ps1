param(
    [string]$Configuration = "Release",
    [string]$Runtime = "",
    [switch]$SingleFile,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "TcBatchRename.csproj"
$outputPath = if ([string]::IsNullOrWhiteSpace($Runtime)) {
    Join-Path $PSScriptRoot "artifacts\publish\portable"
}
else {
    Join-Path $PSScriptRoot "artifacts\publish\$Runtime"
}

$arguments = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-o", $outputPath,
    "-p:RestoreIgnoreFailedSources=true",
    "-p:NuGetAudit=false"
)

if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $arguments += "-r"
    $arguments += $Runtime
}

if ($SingleFile) {
    if ([string]::IsNullOrWhiteSpace($Runtime)) {
        throw "Single-file publish requires -Runtime, for example: -Runtime win-x64 -SingleFile"
    }

    $arguments += "-p:PublishSingleFile=true"
    $arguments += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

# Use the MSBuild property form (-p:SelfContained=...) rather than the
# `--self-contained` CLI flag. With a RuntimeIdentifier set, the CLI flag is not
# reliably honored (it gets overridden back to self-contained on this SDK), which
# silently produced a ~110 MB self-contained exe instead of the intended
# framework-dependent one. The property form is respected.
if ($SelfContained) {
    if ([string]::IsNullOrWhiteSpace($Runtime)) {
        throw "Self-contained publish requires -Runtime, for example: -Runtime win-x64 -SelfContained"
    }

    $arguments += "-p:SelfContained=true"
}
elseif (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $arguments += "-p:SelfContained=false"
}

dotnet @arguments
