#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes git changes and highlights potential issues
.DESCRIPTION
    Reviews staged changes for suspicious patterns like large diffs, whitespace-only changes, 
    debug code, commented code, and files that shouldn't be committed
#>

$ErrorActionPreference = "Stop"

Write-Host "Analyzing staged changes..." -ForegroundColor Cyan
Write-Host ""

# Check if there are staged changes
$stagedFiles = git diff --cached --name-only
if (-not $stagedFiles) {
    Write-Host "WARNING: No staged changes found. Stage your changes with 'git add' first." -ForegroundColor Yellow
    exit 0
}

# Get detailed stats
$stats = git diff --cached --stat
$numstat = git diff --cached --numstat

Write-Host "Change Summary" -ForegroundColor Cyan
Write-Host $stats
Write-Host ""

# Analyze each file
$issues = @()
$warnings = @()

foreach ($line in $numstat) {
    if (-not $line) { continue }
    
    $parts = $line -split "`t"
    if ($parts.Length -lt 3) { continue }
    
    $added = $parts[0]
    $removed = $parts[1]
    $file = $parts[2]
    
    # Skip binary files (marked with -)
    if ($added -eq "-" -or $removed -eq "-") { continue }
    
    $addedNum = [int]$added
    $removedNum = [int]$removed
    $totalChanges = $addedNum + $removedNum
    
    # Flag large changes
    if ($totalChanges -gt 500) {
        $warnings += "WARNING: Large change: $file ($totalChanges lines changed)"
    }
    
    # Flag whitespace-only changes
    if ($addedNum -eq $removedNum -and $totalChanges -gt 0) {
        $diff = git diff --cached $file
        # Extract only content lines (skip diff headers/markers)
        $contentLines = $diff | Where-Object { $_ -match '^[+-][^+-]' }
        # Check if all changes are whitespace
        $nonWhitespaceChanges = $contentLines | Where-Object { $_ -match '^[+-]\S' }
        if ($contentLines -and -not $nonWhitespaceChanges) {
            $warnings += "WARNING: Possible whitespace-only change: $file"
        }
    }
}

# Check for problematic patterns in diff
$fullDiff = git diff --cached

# Check for debug/console statements (avoid false positives for legitimate comments)
$debugPatterns = @(
    '^\+.*console\.log\(',    # JS console.log calls
    '^\+.*debugger;',         # JS debugger statement
    '^\+.*System\.Diagnostics\.Debug\.WriteLine'  # C# Debug.WriteLine calls
)

$hasDebugStatements = $false
foreach ($pattern in $debugPatterns) {
    if ($fullDiff -match $pattern) {
        $hasDebugStatements = $true
        break
    }
}

if ($hasDebugStatements) {
    $issues += "DEBUG: Debug/console statements detected in changes"
}

# Check for consecutive commented-out code blocks (3+ consecutive comment lines suggest actual code)
$lines = $fullDiff -split "`n"
$consecutiveComments = 0
$maxConsecutiveComments = 0
foreach ($line in $lines) {
    if ($line -match '^\+\s*//') {
        $consecutiveComments++
        if ($consecutiveComments -gt $maxConsecutiveComments) {
            $maxConsecutiveComments = $consecutiveComments
        }
    } else {
        $consecutiveComments = 0
    }
}

if ($maxConsecutiveComments -ge 3) {
    $warnings += "WARNING: Potential commented-out code detected ($maxConsecutiveComments consecutive comment lines)"
}

# Check for sensitive files
$sensitivePatterns = @(
    '\.env$',
    'appsettings\.Production\.json$',
    'secrets',
    '\.key$',
    '\.pem$'
)

# Check for unnecessary files that shouldn't be committed
$unnecessaryPatterns = @(
    '^diag.*\.txt$',
    '^output\.txt$',
    '^results\.txt$',
    '^test[_-]?output\.txt$',
    '\.skill$',
    'node_modules/',
    'bin/',
    'obj/',
    '\.vs/',
    '\.vscode/(?!settings\.json|tasks\.json|launch\.json)',
    'TestResults/',
    '__pycache__/',
    '\.pyc$',
    '\.log$',
    '^temp',
    '^tmp'
)

foreach ($file in $stagedFiles) {
    foreach ($pattern in $sensitivePatterns) {
        if ($file -match $pattern) {
            $issues += "SENSITIVE FILE: $file should not be committed!"
        }
    }
    
    foreach ($pattern in $unnecessaryPatterns) {
        if ($file -match $pattern) {
            $warnings += "WARNING: Unnecessary file staged: $file (likely build output or temporary file)"
        }
    }
}

# Report findings
if ($issues.Count -gt 0) {
    Write-Host "Issues Found:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  $issue" -ForegroundColor Red
    }
    Write-Host ""
}

if ($warnings.Count -gt 0) {
    Write-Host "Warnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  $warning" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($issues.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "No obvious issues detected in changes" -ForegroundColor Green
    Write-Host ""
}

# Return exit code
if ($issues.Count -gt 0) {
    exit 1
}

exit 0
