# Smoke Test Script for Doc2MD Converter
# Usage: .\scripts\smoke-test.ps1

param(
    [string]$PublishDir = ".\publish"
)

$base = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $base

Write-Host "=== Doc2MD Converter Smoke Test ===" -ForegroundColor Cyan

$failures = 0

# Test 1: Solution builds
Write-Host "Test 1: Solution build..." -ForegroundColor Yellow
dotnet build Doc2MD.Converter.slnx --configuration Release 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) { Write-Host "  PASS" -ForegroundColor Green }
else { Write-Host "  FAIL" -ForegroundColor Red; $failures++ }

# Test 2: Tests pass
Write-Host "Test 2: Unit tests..." -ForegroundColor Yellow
dotnet test tests/Doc2MD.Converter.Core.Tests/Doc2MD.Converter.Core.Tests.csproj `
    --configuration Release --no-build --verbosity quiet 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) { Write-Host "  PASS" -ForegroundColor Green }
else { Write-Host "  FAIL" -ForegroundColor Red; $failures++ }

# Test 3: GUI publish EXE exists
Write-Host "Test 3: GUI publish EXE exists..." -ForegroundColor Yellow
$guiExe = Join-Path $base "$PublishDir\gui\Doc2MD.Converter.exe"
if (Test-Path $guiExe) { Write-Host "  PASS" -ForegroundColor Green }
else { Write-Host "  FAIL (EXE not found at $guiExe)" -ForegroundColor Red; $failures++ }

# Test 4: Core DLL exists
Write-Host "Test 4: Core DLL exists..." -ForegroundColor Yellow
$coreDll = Join-Path $base "src\Doc2MD.Converter.Core\bin\Release\net8.0\Doc2MD.Converter.Core.dll"
if (Test-Path $coreDll) { Write-Host "  PASS" -ForegroundColor Green }
else { Write-Host "  FAIL" -ForegroundColor Red; $failures++ }

# Test 5: No Doc2KB dependency
Write-Host "Test 5: No Doc2KB dependency..." -ForegroundColor Yellow
$csprojs = Get-ChildItem $base -Recurse -Filter "*.csproj"
$hasDoc2KBRef = $false
foreach ($csproj in $csprojs) {
    $content = Get-Content $csproj.FullName -Raw
    if ($content -match "Doc2KB") { $hasDoc2KBRef = $true; break }
}
if (-not $hasDoc2KBRef) { Write-Host "  PASS" -ForegroundColor Green }
else { Write-Host "  FAIL (found Doc2KB reference)" -ForegroundColor Red; $failures++ }

# Test 6: Self-contained publish properties
Write-Host "Test 6: Self-contained publish..." -ForegroundColor Yellow
$guiExe = Join-Path $base "$PublishDir\gui\Doc2MD.Converter.exe"
if (Test-Path $guiExe) {
    $fileSize = (Get-Item $guiExe).Length / 1MB
    if ($fileSize -gt 50) { Write-Host "  PASS ($([math]::Round($fileSize, 1)) MB)" -ForegroundColor Green }
    else { Write-Host "  FAIL (file too small, likely not self-contained)" -ForegroundColor Red; $failures++ }
} else {
    Write-Host "  SKIP (GUI EXE not published)" -ForegroundColor Yellow
}

# Test 7: No Legacy MD2DOCX engine code remains
Write-Host "Test 7: No Legacy engine remains..." -ForegroundColor Yellow
$legacyMarkers = @("MarkdownToDocxParser", "UsePipelineEngine", "ConversionTarget.OfficialDocx")
$foundLegacy = $false
$sourceFiles = Get-ChildItem "$base\src" -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
foreach ($sf in $sourceFiles) {
    $content = Get-Content $sf.FullName -Raw
    foreach ($marker in $legacyMarkers) {
        if ($content -match $marker) {
            Write-Host "  Found '$marker' in $($sf.FullName)" -ForegroundColor Red
            $foundLegacy = $true
        }
    }
}
if (-not $foundLegacy) { Write-Host "  PASS" -ForegroundColor Green }
else { Write-Host "  FAIL" -ForegroundColor Red; $failures++ }

# Test 8: No CLI project remains
Write-Host "Test 8: No CLI project remains..." -ForegroundColor Yellow
$cliDir = Join-Path $base "src\Doc2MD.Converter.Cli"
$cliPublishDir = Join-Path $base "publish\cli"
$cliRemoved = $true
if (Test-Path $cliDir) { Write-Host "  FAIL (CLI project dir exists)" -ForegroundColor Red; $cliRemoved = $false; $failures++ }
if (Test-Path $cliPublishDir) { Write-Host "  FAIL (CLI publish dir exists)" -ForegroundColor Red; $cliRemoved = $false; $failures++ }
if ($cliRemoved) { Write-Host "  PASS" -ForegroundColor Green }

# Summary
Write-Host ""
if ($failures -eq 0) {
    Write-Host "=== All smoke tests passed ===" -ForegroundColor Green
} else {
    Write-Host "=== $failures smoke test(s) failed ===" -ForegroundColor Red
}
exit $failures
