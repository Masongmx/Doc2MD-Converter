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

# Test 3: CLI runs
Write-Host "Test 3: CLI runs..." -ForegroundColor Yellow
$cliExe = Join-Path $base "src\Doc2MD.Converter.Cli\bin\Release\net8.0\doc2md-converter.dll"
if (Test-Path $cliExe) {
    $result = dotnet $cliExe --help 2>&1
    if ($result -match "Doc2MD Converter") { Write-Host "  PASS" -ForegroundColor Green }
    else { Write-Host "  FAIL (unexpected output)" -ForegroundColor Red; $failures++ }
} else {
    Write-Host "  SKIP (CLI not built)" -ForegroundColor Yellow
}

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

# Summary
Write-Host ""
if ($failures -eq 0) {
    Write-Host "=== All smoke tests passed ===" -ForegroundColor Green
} else {
    Write-Host "=== $failures smoke test(s) failed ===" -ForegroundColor Red
}
exit $failures
