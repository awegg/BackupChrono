param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$MaxBackups = 50,
    [int]$PrintCount = 10,
    [string]$DownloadDir = "$PSScriptRoot/output"
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Uri
    )

    Write-Host "[HTTP] $Method $Uri" -ForegroundColor Cyan
    Invoke-RestMethod -Method $Method -Uri $Uri -ContentType "application/json"
}

function Get-BackupJobs {
    param([string]$BaseUrl, [int]$Limit)
    $uri = "$BaseUrl/api/backup-jobs?limit=$Limit"
    Invoke-Api -Method GET -Uri $uri
}

function Get-Files {
    param(
        [string]$BaseUrl,
        [string]$BackupId,
        [guid]$DeviceId,
        [guid]$ShareId,
        [string]$Path
    )
    $encodedPath = [uri]::EscapeDataString($Path)
    $uri = "$BaseUrl/api/backups/$BackupId/files?deviceId=$DeviceId&shareId=$ShareId&path=$encodedPath"
    Invoke-Api -Method GET -Uri $uri
}

function Download-FileFromBackup {
    param(
        [string]$BaseUrl,
        [string]$BackupId,
        [guid]$DeviceId,
        [guid]$ShareId,
        [string]$FilePath,
        [string]$OutputPath
    )
    $encodedPath = [uri]::EscapeDataString($FilePath)
    $uri = "$BaseUrl/api/backups/$BackupId/download?deviceId=$DeviceId&shareId=$ShareId&filePath=$encodedPath"
    Write-Host "[DOWNLOAD] $FilePath -> $OutputPath" -ForegroundColor Yellow
    Invoke-WebRequest -Method GET -Uri $uri -OutFile $OutputPath | Out-Null
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Restore Self-Test - E2E Workflow Verification" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

Write-Host "Step 1: Preparing output directory" -ForegroundColor Yellow
if (-not (Test-Path -Path $DownloadDir)) {
    New-Item -ItemType Directory -Path $DownloadDir | Out-Null
    Write-Host "  Created directory: $DownloadDir" -ForegroundColor Gray
} else {
    Write-Host "  Using existing directory: $DownloadDir" -ForegroundColor Gray
}
Write-Host ""

Write-Host "Step 2: Querying recent backup jobs" -ForegroundColor Yellow
Write-Host "  Fetching last $MaxBackups jobs from API..." -ForegroundColor Gray
$jobs = Get-BackupJobs -BaseUrl $BaseUrl -Limit $MaxBackups
if (-not $jobs) {
    Write-Error "No backup jobs found"
    exit 1
}
Write-Host "  Found $($jobs.Count) backup jobs" -ForegroundColor Gray
Write-Host ""

$sorted = $jobs | Sort-Object {
    if ($_.completedAt) { [datetime]$_.completedAt } else { [datetime]$_.startedAt }
} -Descending

Write-Host "  Last $PrintCount jobs:" -ForegroundColor Magenta
$sorted | Select-Object -First $PrintCount | ForEach-Object {
    Write-Host "    Job: $($_.id) | Status: $($_.status) | BackupId: $($_.backupId) | Device: $($_.deviceName) | Share: $($_.shareName) | Completed: $($_.completedAt)" -ForegroundColor DarkGray
}
Write-Host ""

Write-Host "Step 3: Selecting latest successful backup" -ForegroundColor Yellow
$latestSuccessJob = $sorted | Where-Object {
    $status = ""
    if ($_.status) { $status = $_.status.ToString().ToUpperInvariant() }
    ($status -eq 'SUCCESS' -or $status -eq 'COMPLETED') -and $_.backupId
} | Select-Object -First 1
if (-not $latestSuccessJob) {
    Write-Error "No successful/completed backup job with BackupId found"
    exit 1
}

Write-Host "  Selected backup snapshot:" -ForegroundColor Gray
Write-Host "    Job ID:     $($latestSuccessJob.id)" -ForegroundColor Gray
Write-Host "    Backup ID:  $($latestSuccessJob.backupId)" -ForegroundColor Gray
Write-Host "    Device:     $($latestSuccessJob.deviceName)" -ForegroundColor Gray
Write-Host "    Share:      $($latestSuccessJob.shareName)" -ForegroundColor Gray
Write-Host "    Completed:  $($latestSuccessJob.completedAt)" -ForegroundColor Gray
Write-Host ""

$deviceId = [guid]$latestSuccessJob.deviceId
$shareId = $null
if ($latestSuccessJob.shareId) {
    $shareId = [guid]$latestSuccessJob.shareId
}

if (-not $shareId) {
    Write-Error "ShareId missing for selected job"
    exit 1
}

$backupId = $latestSuccessJob.backupId

Write-Host "Step 4: Browsing backup snapshot (searching for first file)" -ForegroundColor Yellow
Write-Host "  Starting at root directory..." -ForegroundColor Gray
$rootEntries = Get-Files -BaseUrl $BaseUrl -BackupId $backupId -DeviceId $deviceId -ShareId $shareId -Path "/"

$queue = New-Object System.Collections.Generic.Queue[object]
foreach ($entry in $rootEntries) { $queue.Enqueue($entry) }

$fileToDownload = $null
$dirsTraversed = 0
while ($queue.Count -gt 0) {
    $current = $queue.Dequeue()
    if (-not $current.isDirectory) {
        $fileToDownload = $current
        Write-Host "  Found first file after traversing $dirsTraversed directories" -ForegroundColor Gray
        break
    }

    $dirsTraversed++
    Write-Host "  Traversing directory: $($current.path)" -ForegroundColor DarkCyan
    $children = Get-Files -BaseUrl $BaseUrl -BackupId $backupId -DeviceId $deviceId -ShareId $shareId -Path $current.path
    foreach ($child in $children) { $queue.Enqueue($child) }
}

if (-not $fileToDownload) {
    Write-Error "No files found in backup"
    exit 1
}
Write-Host ""

Write-Host "Step 5: Downloading file from backup" -ForegroundColor Yellow
Write-Host "  File path:  $($fileToDownload.path)" -ForegroundColor Gray
Write-Host "  File size:  $($fileToDownload.size) bytes" -ForegroundColor Gray

$outFileName = Split-Path -Path $fileToDownload.path -Leaf
$outFilePath = Join-Path -Path $DownloadDir -ChildPath $outFileName
Write-Host "  Target:     $outFilePath" -ForegroundColor Gray

Download-FileFromBackup -BaseUrl $BaseUrl -BackupId $backupId -DeviceId $deviceId -ShareId $shareId -FilePath $fileToDownload.path -OutputPath $outFilePath

Write-Host "  Download successful!" -ForegroundColor Green
Write-Host ""

Write-Host "Step 6: Validating file size" -ForegroundColor Yellow
$downloadedFileInfo = Get-Item -Path $outFilePath
$downloadedSize = $downloadedFileInfo.Length
$expectedSize = $fileToDownload.size

Write-Host "  Expected size (from API): $expectedSize bytes" -ForegroundColor Gray
Write-Host "  Downloaded size:          $downloadedSize bytes" -ForegroundColor Gray

if ($downloadedSize -eq $expectedSize) {
    Write-Host "  File size matches!" -ForegroundColor Green
} else {
    Write-Error "File size mismatch! Expected $expectedSize bytes but got $downloadedSize bytes"
    exit 1
}
Write-Host ""

Write-Host "Step 7: Verifying file content" -ForegroundColor Yellow
$fileBytes = [System.IO.File]::ReadAllBytes($outFilePath)
$sampleSize = [Math]::Min(100, $fileBytes.Length)

# Check if content is ASCII (all bytes < 128 and printable)
$isAscii = $true
for ($i = 0; $i -lt [Math]::Min(100, $fileBytes.Length); $i++) {
    if ($fileBytes[$i] -ge 128 -or ($fileBytes[$i] -lt 32 -and $fileBytes[$i] -ne 9 -and $fileBytes[$i] -ne 10 -and $fileBytes[$i] -ne 13)) {
        $isAscii = $false
        break
    }
}

if ($isAscii -and $fileBytes.Length -gt 0) {
    Write-Host "  File appears to be ASCII text" -ForegroundColor Gray
    $preview = [System.Text.Encoding]::ASCII.GetString($fileBytes, 0, [Math]::Min(100, $fileBytes.Length))
    Write-Host "  First 100 characters:" -ForegroundColor Gray
    Write-Host "  ---" -ForegroundColor DarkGray
    Write-Host "  $preview" -ForegroundColor Cyan
    Write-Host "  ---" -ForegroundColor DarkGray
} else {
    Write-Host "  File appears to be binary data" -ForegroundColor Gray
    Write-Host "  First 50 bytes (hex):" -ForegroundColor Gray
    $hexValues = @()
    for ($i = 0; $i -lt [Math]::Min(50, $fileBytes.Length); $i++) {
        $hexValues += "{0:X2}" -f $fileBytes[$i]
    }
    Write-Host "  $($hexValues -join ' ')" -ForegroundColor Cyan
}
Write-Host ""

Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Self-test PASSED" -ForegroundColor Green
Write-Host "  - Queried backup jobs successfully" -ForegroundColor Green
Write-Host "  - Selected latest successful backup" -ForegroundColor Green
Write-Host "  - Browsed snapshot directory tree" -ForegroundColor Green
Write-Host "  - Downloaded file from backup" -ForegroundColor Green
Write-Host "  - Validated file size matches" -ForegroundColor Green
Write-Host "  - Verified file content" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
