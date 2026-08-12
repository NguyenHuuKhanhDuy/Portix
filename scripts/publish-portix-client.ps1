<#
.SYNOPSIS
    Publishes Portix.Client as a single, self-contained portix.exe for Windows (win-x64) —
    no companion DLLs, no wwwroot/ or appsettings.json alongside it, no .NET runtime required
    on the machine it runs on.
#>
param(
    [string]$OutputDir = "publish/portix-client-win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot "src/Portix.Client/Portix.Client.csproj"
$resolvedOutputDir = Join-Path $repoRoot $OutputDir

dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $resolvedOutputDir

$exePath = Join-Path $resolvedOutputDir "portix.exe"
if (-not (Test-Path $exePath)) {
    throw "Publish completed but portix.exe was not found at $exePath"
}

Write-Host ""
Write-Host "Published: $exePath" -ForegroundColor Green
