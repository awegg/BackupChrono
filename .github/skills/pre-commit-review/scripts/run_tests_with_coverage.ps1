#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs backend tests with code coverage analysis
.DESCRIPTION
    Executes the .NET test suite and checks that code coverage meets the minimum threshold of 50%
.PARAMETER MinimumCoverage
    Minimum acceptable code coverage percentage (default: 50)
#>

param(
    [int]$MinimumCoverage = 50
)

$ErrorActionPreference = "Stop"

Write-Host "Running tests with coverage analysis..." -ForegroundColor Cyan
Write-Host "Minimum coverage threshold: $MinimumCoverage%" -ForegroundColor Cyan
Write-Host ""

# Navigate to backend directory using more robust git-root detection
try {
    $gitRoot = & git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Not in a git repository"
    }
} catch {
    Write-Host "WARNING: Could not determine git root, using relative paths" -ForegroundColor Yellow
    # Fallback: assume script location and navigate up
    $scriptDir = Split-Path -Parent $PSScriptRoot
    $skillsDir = Split-Path -Parent $scriptDir
    $githubDir = Split-Path -Parent $skillsDir
    $gitRoot = Split-Path -Parent $githubDir
}

$backendDir = Join-Path $gitRoot "src" "backend"
Push-Location $backendDir

try {
    # Run tests with coverage
    $testCommand = "dotnet test --collect:'XPlat Code Coverage' --results-directory ./TestResults"
    Write-Host "Executing: $testCommand" -ForegroundColor Gray
    
    $testOutput = & dotnet test --collect:'XPlat Code Coverage' --results-directory ./TestResults 2>&1
    $testExitCode = $LASTEXITCODE
    
    Write-Host $testOutput
    
    if ($testExitCode -ne 0) {
        Write-Host "`nTests failed!" -ForegroundColor Red
        exit $testExitCode
    }
    
    Write-Host "`nAll tests passed!" -ForegroundColor Green
    
    # Find the latest coverage file
    $coverageFiles = Get-ChildItem -Path ./TestResults -Recurse -Filter "coverage.cobertura.xml" | Sort-Object LastWriteTime -Descending
    
    if ($coverageFiles.Count -eq 0) {
        Write-Host "ERROR: No coverage report found" -ForegroundColor Red
        Write-Host "Tests may have run but coverage collection failed" -ForegroundColor Yellow
        exit 1
    }
    
    $latestCoverage = $coverageFiles[0].FullName
    Write-Host "`nAnalyzing coverage from: $($coverageFiles[0].Name)" -ForegroundColor Gray
    
    # Parse coverage percentage from XML
    [xml]$coverageXml = Get-Content $latestCoverage
    $lineCoverage = [double]$coverageXml.coverage.'line-rate' * 100
    
    Write-Host "`nCode Coverage: $([math]::Round($lineCoverage, 2))%" -ForegroundColor Cyan
    
    if ($lineCoverage -lt $MinimumCoverage) {
        Write-Host "FAILED: Coverage is below minimum threshold of $MinimumCoverage%" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "PASSED: Coverage meets minimum threshold!" -ForegroundColor Green
    }
    
} finally {
    Pop-Location
}
