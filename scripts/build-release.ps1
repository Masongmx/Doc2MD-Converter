# Build Release Script for Doc2MD Converter
# Usage: .\scripts\build-release.ps1

param(
    [string]$Configuration = "Release",
    [string]$Version = "2.1.0"
)

$ErrorActionPreference = "Stop"
$base = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $base

Write-Host "=== Doc2MD Converter Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Version: $Version"
Write-Host "Base: $base"
Write-Host ""

# Step 1: Clean
Write-Host "Step 1: Cleaning old build artifacts..." -ForegroundColor Yellow
dotnet clean Doc2MD.Converter.slnx --configuration $Configuration /p:Version=$Version 2>&1 | Out-Null

# Step 2: Build
Write-Host "Step 2: Building solution..." -ForegroundColor Yellow
dotnet build Doc2MD.Converter.slnx --configuration $Configuration /p:Version=$Version
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Build succeeded." -ForegroundColor Green

# Step 3: Test
Write-Host "Step 3: Running tests..." -ForegroundColor Yellow
dotnet test tests/Doc2MD.Converter.Core.Tests/Doc2MD.Converter.Core.Tests.csproj `
    --configuration $Configuration --no-build --verbosity minimal
if ($LASTEXITCODE -ne 0) { Write-Host "TESTS FAILED" -ForegroundColor Red; exit 1 }
Write-Host "All tests passed." -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host "Output: $base\src\Doc2MD.Converter.App\bin\$Configuration\net8.0-windows\win-x64\"
