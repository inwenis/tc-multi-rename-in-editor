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

if ($SelfContained) {
    if ([string]::IsNullOrWhiteSpace($Runtime)) {
        throw "Self-contained publish requires -Runtime, for example: -Runtime win-x64 -SelfContained"
    }

    $arguments += "--self-contained"
    $arguments += "true"
}
elseif (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $arguments += "--self-contained"
    $arguments += "false"
}

dotnet @arguments
