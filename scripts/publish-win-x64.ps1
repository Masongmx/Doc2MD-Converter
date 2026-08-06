# Publish Script for Doc2MD Converter (win-x64 self-contained)
# Usage: .\scripts\publish-win-x64.ps1

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$OutputDir = ".\publish"
)

$ErrorActionPreference = "Stop"
$base = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $base

# Build and test first
& ".\scripts\build-release.ps1" -Configuration $Configuration -Version $Version
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host ""
Write-Host "=== Publishing Doc2MD Converter ===" -ForegroundColor Cyan

$publishDir = Join-Path $base $OutputDir
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

# Publish App (WPF, self-contained single-file)
Write-Host "Publishing WPF App..." -ForegroundColor Yellow
dotnet publish src/Doc2MD.Converter.App/Doc2MD.Converter.App.csproj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    --output "$publishDir\gui"

# Publish CLI (self-contained single-file)
Write-Host "Publishing CLI..." -ForegroundColor Yellow
dotnet publish src/Doc2MD.Converter.Cli/Doc2MD.Converter.Cli.csproj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    --output "$publishDir\cli"

# Copy templates and docs
if (Test-Path "$base\templates") {
    Copy-Item "$base\templates" "$publishDir\templates" -Recurse -Force
    Write-Host "Templates copied." -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "=== Publish Complete ===" -ForegroundColor Cyan
$appSize = (Get-ChildItem "$publishDir\gui" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$cliSize = (Get-ChildItem "$publishDir\cli" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "GUI: $publishDir\gui\ ($([math]::Round($appSize, 1)) MB)"
Write-Host "CLI: $publishDir\cli\ ($([math]::Round($cliSize, 1)) MB)"
