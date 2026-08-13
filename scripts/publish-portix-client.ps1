<#
.SYNOPSIS
    Publishes Portix.Client as a single, self-contained executable — no companion DLLs, no
    wwwroot/ alongside it, no .NET runtime required on the machine it runs on. Builds for
    Windows, macOS Intel, and macOS Apple Silicon by default (cross-compiles fine from Windows —
    no native toolchain needed, just the target's runtime pack via NuGet).

.PARAMETER Rid
    One or more .NET RIDs to publish for. Defaults to all three release targets.

.PARAMETER Version
    Version to embed in the published assembly (AssemblyVersion/FileVersion/InformationalVersion),
    passed through as `dotnet publish -p:Version=...`. Optional — omitting it leaves the SDK's
    own default versioning behavior in place.

.EXAMPLE
    ./scripts/publish-portix-client.ps1
    Builds win-x64, osx-x64, and osx-arm64.

.EXAMPLE
    ./scripts/publish-portix-client.ps1 -Rid win-x64
    Builds just Windows.

.EXAMPLE
    ./scripts/publish-portix-client.ps1 -Version 1.2.0
    Builds all three targets with version 1.2.0 embedded.
#>
param(
    [string[]]$Rid = @("win-x64", "osx-x64", "osx-arm64"),
    [string]$OutputRoot = "publish",
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "src/Portix.Client/Portix.Client.csproj"

$published = @()

foreach ($r in $Rid) {
    $outputDir = Join-Path $repoRoot "$OutputRoot/portix-client-$r"

    Write-Host ""
    Write-Host "Publishing for $r..." -ForegroundColor Cyan
    $publishArgs = @(
        $csproj,
        "-c", "Release",
        "-r", $r,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-o", $outputDir
    )
    if ($Version) {
        $publishArgs += "-p:Version=$Version"
    }
    dotnet publish @publishArgs

    # .NET drops the .exe extension for non-Windows RIDs; AssemblyName is "portix" either way.
    $exeName = if ($r.StartsWith("win-")) { "portix.exe" } else { "portix" }
    $exePath = Join-Path $outputDir $exeName
    if (-not (Test-Path $exePath)) {
        throw "Publish completed for $r but $exeName was not found at $exePath"
    }

    $published += $exePath
}

Write-Host ""
Write-Host "Published:" -ForegroundColor Green
foreach ($path in $published) {
    Write-Host "  $path" -ForegroundColor Green
}
