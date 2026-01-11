using System.Text.Json;
using System.Diagnostics;
using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Restic;

/// <summary>
/// Implementation of IResticService for backup operations using restic.
/// </summary>
public class ResticService : IResticService
{
    private readonly IResticClient _client;
    private readonly ILogger<ResticService> _logger;
    private static readonly TimeSpan LogThrottleInterval = TimeSpan.FromSeconds(1);

    public ResticService(IResticClient client, ILogger<ResticService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> InitializeRepository(string repositoryPath, string password)
    {
        try
        {
            await _client.ExecuteCommand(new[] { "init" }, repositoryPathOverride: repositoryPath);
            return true;
        }
        catch (Exception ex) when (ex.Message.Contains("already initialized") || 
                                     ex.Message.Contains("config file already exists"))
        {
            // Repository already exists, which is acceptable
            return false;
        }
        // Let other exceptions propagate for proper error handling
    }

    public async Task<bool> RepositoryExists(string repositoryPath)
    {
        try
        {
            var output = await _client.ExecuteCommand(new[] { "snapshots", "--json" }, repositoryPathOverride: repositoryPath);
            _logger.LogInformation(output);
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Is there a repository at the following location?") || 
                                                     ex.Message.Contains("unable to open repository") ||
                                                     ex.Message.Contains("repository does not exist"))
        {
            // Repository not initialized - this is expected when checking existence
            return false;
        }
        // Let other exceptions (network failures, permission issues, corrupted repos) propagate
    }

    public async Task<RepositoryStats> GetStats(string repositoryPath)
    {
        var output = await _client.ExecuteCommand(new[] { "stats", "--json" }, repositoryPathOverride: repositoryPath);
        // TODO: Parse JSON output into RepositoryStats
        return new RepositoryStats
        {
            TotalSize = 0,
            UniqueSize = 0,
            BackupCount = 0,
            FileCount = 0
        };
    }

    public async Task VerifyIntegrity(string repositoryPath)
    {
        await _client.ExecuteCommand(new[] { "check" }, repositoryPathOverride: repositoryPath);
    }

    public async Task Prune(string repositoryPath)
    {
        await _client.ExecuteCommand(new[] { "prune" }, repositoryPathOverride: repositoryPath);
    }

    public async Task<Backup> CreateBackup(string repositoryPath, Device device, Share? share, string sourcePath, IncludeExcludeRules rules, Action<BackupProgress>? onProgress = null, Action<string>? onWarning = null, Action<string>? onError = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "backup", sourcePath, "--json" };

        // Add exclude patterns
        foreach (var pattern in rules.ExcludePatterns)
        {
            args.Add("--exclude");
            args.Add(pattern);
        }

        // Local throttle state for this backup job
        var lastLogTime = DateTime.MinValue;

        var output = await _client.ExecuteCommand(args.ToArray(), cancellationToken, onOutputLine: line =>
        {
            if (onProgress == null || string.IsNullOrWhiteSpace(line)) return;
            
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                
                // Log all message types for debugging
                if (root.TryGetProperty("message_type", out var messageType))
                {
                    var msgType = messageType.GetString();
                    _logger.LogTrace("Restic message type: {MessageType}, Content: {Content}", msgType, line);
                }
                
                if (root.TryGetProperty("message_type", out var statusMessageType) && 
                    statusMessageType.GetString() == "status")
                {
                    var progress = new BackupProgress
                    {
                        JobId = "", // Will be set by BackupOrchestrator
                        DeviceName = device.Name,
                        ShareName = share?.Name,
                        Status = "Running",
                        PercentComplete = root.TryGetProperty("percent_done", out var percentDone) 
                            ? percentDone.GetDouble() * 100 
                            : 0,
                        FilesProcessed = root.TryGetProperty("files_done", out var filesDone) 
                            ? filesDone.GetInt32() 
                            : 0,
                        TotalFiles = root.TryGetProperty("total_files", out var totalFiles) 
                            ? totalFiles.GetInt32() 
                            : null,
                        BytesProcessed = root.TryGetProperty("bytes_done", out var bytesDone) 
                            ? bytesDone.GetInt64() 
                            : 0,
                        TotalBytes = root.TryGetProperty("total_bytes", out var totalBytes) 
                            ? totalBytes.GetInt64() 
                            : null,
                        CurrentFile = root.TryGetProperty("current_files", out var currentFiles) && 
                                      currentFiles.GetArrayLength() > 0
                            ? currentFiles[0].GetString()
                            : null
                    };
                    
                    var now = DateTime.UtcNow;
                    if (now - lastLogTime >= LogThrottleInterval)
                    {
                        _logger.LogDebug("Restic progress: {Percent}% - {Files}/{TotalFiles} files", 
                            progress.PercentComplete, progress.FilesProcessed, progress.TotalFiles);
                        lastLogTime = now;
                    }
                    
                    onProgress(progress);
                }
            }
            catch (JsonException)
            {
                // Ignore JSON parse errors for non-JSON output lines (restic outputs mixed text/JSON)
            }
        }, repositoryPathOverride: repositoryPath, onErrorLine: line =>
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // Forward stderr lines to optional callbacks for warnings/errors
            if (line.Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                onWarning?.Invoke(line);
            }
            else
            {
                onError?.Invoke(line);
            }
        });
        
        // Parse backup summary from last JSON line
        string? snapshotId = null;
        int filesNew = 0, filesChanged = 0, filesUnmodified = 0;
        long dataAdded = 0, dataProcessed = 0;
        
        try
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            // Find the summary message (last message_type=summary line)
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                try
                {
                    using var doc = JsonDocument.Parse(lines[i]);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("message_type", out var msgType) && 
                        msgType.GetString() == "summary")
                    {
                        snapshotId = root.TryGetProperty("snapshot_id", out var snapId) 
                            ? snapId.GetString()?[..8] // Use short ID format
                            : null;
                        
                        filesNew = root.TryGetProperty("files_new", out var fNew) ? fNew.GetInt32() : 0;
                        filesChanged = root.TryGetProperty("files_changed", out var fChanged) ? fChanged.GetInt32() : 0;
                        filesUnmodified = root.TryGetProperty("files_unmodified", out var fUnmod) ? fUnmod.GetInt32() : 0;
                        dataAdded = root.TryGetProperty("data_added", out var dAdded) ? dAdded.GetInt64() : 0;
                        dataProcessed = root.TryGetProperty("total_bytes_processed", out var dProc) ? dProc.GetInt64() : 0;
                        
                        break;
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse backup summary for snapshot ID extraction");
        }
        
        if (string.IsNullOrEmpty(snapshotId))
        {
            _logger.LogWarning("Snapshot ID not found in restic output, generating fallback ID");
            snapshotId = Guid.NewGuid().ToString("N")[..8];
        }
        
        return new Backup
        {
            Id = snapshotId,
            DeviceId = device.Id,
            ShareId = share?.Id,
            DeviceName = device.Name,
            ShareName = share?.Name,
            Timestamp = DateTime.UtcNow,
            Status = BackupStatus.Success,
            FilesNew = filesNew,
            FilesChanged = filesChanged,
            FilesUnmodified = filesUnmodified,
            DataAdded = dataAdded,
            DataProcessed = dataProcessed,
            Duration = TimeSpan.Zero
        };
    }

    public Task<BackupProgress> GetBackupProgress(string jobId)
    {
        // TODO: Implement progress tracking
        return Task.FromResult(new BackupProgress
        {
            JobId = jobId,
            FilesProcessed = 0,
            BytesProcessed = 0,
            PercentComplete = 0
        });
    }

    public Task CancelBackup(string jobId)
    {
        // TODO: Implement backup cancellation
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Backup>> ListBackups(string? deviceName = null, string? repositoryPath = null)
    {
        var args = new List<string> { "snapshots", "--json" };
        
        if (deviceName != null)
        {
            args.Add("--host");
            args.Add(deviceName);
        }

        var output = await _client.ExecuteCommand(args.ToArray(), repositoryPathOverride: repositoryPath);
        
        // Parse snapshots from JSON
        var backups = new List<Backup>();
        
        try
        {
            using var doc = JsonDocument.Parse(output);
            var snapshots = doc.RootElement;
            
            foreach (var snapshot in snapshots.EnumerateArray())
            {
                var id = snapshot.GetProperty("short_id").GetString() ?? string.Empty;
                var hostname = snapshot.GetProperty("hostname").GetString() ?? "unknown";
                var timestamp = snapshot.GetProperty("time").GetDateTime();
                
                // Parse tags for device/share metadata
                var tags = new List<string>();
                if (snapshot.TryGetProperty("tags", out var tagsElement))
                {
                    tags = tagsElement.EnumerateArray().Select(t => t.GetString() ?? "").ToList();
                }
                
                backups.Add(new Backup
                {
                    Id = id,
                    DeviceId = Guid.Empty, // Will be populated from tags or metadata
                    ShareId = null,
                    DeviceName = hostname,
                    ShareName = null,
                    Timestamp = timestamp,
                    Status = BackupStatus.Success,
                    FilesNew = 0, // Summary stats not available in snapshot list
                    FilesChanged = 0,
                    FilesUnmodified = 0,
                    DataAdded = 0,
                    DataProcessed = 0,
                    Duration = TimeSpan.Zero
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse backup snapshots from restic output");
            throw;
        }
        
        return backups;
    }

    public async Task<Backup> GetBackup(string backupId, string? repositoryPath = null)
    {
        _logger.LogInformation("GetBackup called: backupId={BackupId}, repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "default");
        
        // First get metadata which includes snapshot details
        var metadata = await GetSnapshotMetadata(backupId, repositoryPath);
        
        // Get snapshot stats
        var statsArgs = new[] { "stats", backupId, "--json" };
        var statsOutput = await _client.ExecuteCommand(statsArgs, repositoryPathOverride: repositoryPath);
        
        long totalSize = 0;
        int totalFileCount = 0;
        
        try
        {
            using var statsDoc = JsonDocument.Parse(statsOutput);
            var stats = statsDoc.RootElement;
            totalSize = stats.TryGetProperty("total_size", out var size) ? size.GetInt64() : 0;
            totalFileCount = stats.TryGetProperty("total_file_count", out var count) ? count.GetInt32() : 0;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse backup stats for {BackupId}", backupId);
        }
        
        return new Backup
        {
            Id = metadata.Id,
            DeviceId = Guid.Empty,
            ShareId = null,
            DeviceName = metadata.Hostname,
            ShareName = null,
            Timestamp = metadata.Time,
            Status = BackupStatus.Success,
            FilesNew = metadata.Summary?.FilesNew ?? 0,
            FilesChanged = metadata.Summary?.FilesChanged ?? 0,
            FilesUnmodified = metadata.Summary?.FilesUnmodified ?? totalFileCount,
            DataAdded = metadata.Summary?.DataAdded ?? 0,
            DataProcessed = metadata.Summary?.DataProcessed ?? totalSize,
            Duration = TimeSpan.Zero
        };
    }

    public async Task<SnapshotMetadata> GetSnapshotMetadata(string backupId, string? repositoryPath = null)
    {
        var args = new[] { "snapshots", backupId, "--json" };
        var output = await _client.ExecuteCommand(args, repositoryPathOverride: repositoryPath);
        
        try
        {
            using var doc = JsonDocument.Parse(output);
            var snapshots = doc.RootElement;
            
            if (snapshots.GetArrayLength() == 0)
            {
                throw new KeyNotFoundException($"Snapshot {backupId} not found");
            }
            
            var snapshot = snapshots[0];
            return ParseSnapshotMetadata(snapshot, backupId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse snapshot metadata for {BackupId}", backupId);
            throw;
        }
    }

    public async Task<SnapshotStats> GetSnapshotStats(string backupId, string? repositoryPath = null)
    {
        var restoreStatsTask = _client.ExecuteCommand(new[] { "stats", backupId, "--json", "--mode=restore-size" }, repositoryPathOverride: repositoryPath);
        var rawDataStatsTask = _client.ExecuteCommand(new[] { "stats", backupId, "--json", "--mode=raw-data" }, repositoryPathOverride: repositoryPath);
        var blobStatsTask = _client.ExecuteCommand(new[] { "stats", backupId, "--json", "--mode=blobs-per-file" }, repositoryPathOverride: repositoryPath);

        await Task.WhenAll(restoreStatsTask, rawDataStatsTask, blobStatsTask);

        var restoreOutput = await restoreStatsTask;
        var blobOutput = await blobStatsTask;
        string rawDataOutput = string.Empty;
        try
        {
            rawDataOutput = await rawDataStatsTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Raw-data stats unavailable for {BackupId}, falling back to restore-size only", backupId);
        }

        long logicalSize = 0;
        long rawDataSize = 0;
        long totalBlobCount = 0;
        long totalTreeCount = 0;

        try
        {
            using var doc = JsonDocument.Parse(restoreOutput);
            var stats = doc.RootElement;
            logicalSize = stats.TryGetProperty("total_size", out var ts) ? ts.GetInt64() : 0;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse restore-size stats for {BackupId}", backupId);
        }

        try
        {
            using var rawDoc = JsonDocument.Parse(rawDataOutput);
            var rawStats = rawDoc.RootElement;
            rawDataSize = rawStats.TryGetProperty("total_size", out var rs) ? rs.GetInt64() : 0;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse raw-data stats for {BackupId}", backupId);
        }

        try
        {
            using var blobDoc = JsonDocument.Parse(blobOutput);
            var blobStats = blobDoc.RootElement;
            totalBlobCount = blobStats.TryGetProperty("total_blob_count", out var tbc) ? tbc.GetInt64() : 0;
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse blob stats for {BackupId}", backupId);
        }

        var effectiveLogicalSize = logicalSize;
        var effectiveRawSize = rawDataSize > 0 ? rawDataSize : effectiveLogicalSize;
        var spaceSaved = Math.Max(0, effectiveLogicalSize - effectiveRawSize);
        var deduplicationRatio = effectiveLogicalSize > 0
            ? Math.Min(1.0, Math.Max(0.0, (double)spaceSaved / effectiveLogicalSize))
            : 0.0;

        return new SnapshotStats
        {
            SnapshotId = backupId,
            TotalBlobCount = totalBlobCount,
            TotalTreeCount = totalTreeCount,
            TotalSize = effectiveRawSize,
            TotalUncompressedSize = effectiveLogicalSize,
            CompressionRatio = 0.0,
            DeduplicationSpaceSaved = spaceSaved,
            DeduplicationRatio = deduplicationRatio
        };
        
        
    }

    /// <summary>
    /// Get complete backup details including snapshots, metadata, and stats in a single efficient call.
    /// This method combines GetBackup, GetSnapshotMetadata, and GetSnapshotStats into one to minimize restic calls.
    /// </summary>
    public async Task<(Backup Backup, SnapshotMetadata Metadata, SnapshotStats Stats)> GetBackupDetailComplete(string backupId, string? repositoryPath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetBackupDetailComplete called: backupId={BackupId}, repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "default");

        var snapshotTimeout = TimeSpan.FromSeconds(45);
        var statsTimeout = TimeSpan.FromSeconds(15);

        // Get snapshot details - this is the base query
        var snapshotsArgs = new[] { "snapshots", backupId, "--json" };
        var snapshotsOutput = await _client.ExecuteCommand(snapshotsArgs, cancellationToken, snapshotTimeout, repositoryPathOverride: repositoryPath);
        
        // Parse snapshots once
        SnapshotMetadata metadata;
        try
        {
            using var doc = JsonDocument.Parse(snapshotsOutput);
            var snapshots = doc.RootElement;
            
            if (snapshots.GetArrayLength() == 0)
            {
                _logger.LogError("No snapshots found for backupId={BackupId}, repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "default");
                throw new KeyNotFoundException($"Backup {backupId} not found");
            }
            
            var snapshot = snapshots[0];
            metadata = ParseSnapshotMetadata(snapshot, backupId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse snapshot metadata for {BackupId}", backupId);
            throw;
        }

        // Try to get stats without blocking the entire request on slow restic calls
        async Task<string?> TryGetStats(string label, string[] args, TimeSpan timeout)
        {
            try
            {
                var output = await _client.ExecuteCommand(args, cancellationToken, timeout, repositoryPathOverride: repositoryPath);
                return output;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "{Label} timed out for {BackupId}", label, backupId);
                return null;
            }
            catch (OperationCanceledException)
            {
                // Respect caller cancellation (e.g., request aborted)
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Label} failed for {BackupId}", label, backupId);
                return null;
            }
        }

        // Keep stats optional; fall back to zeros if the calls are slow or fail
        var statsOutput = await TryGetStats("stats default", new[] { "stats", backupId, "--json" }, statsTimeout);
        var rawDataOutput = await TryGetStats("stats raw-data", new[] { "stats", backupId, "--json", "--mode=raw-data" }, TimeSpan.FromSeconds(10));
        var blobsOutput = await TryGetStats("stats blobs-per-file", new[] { "stats", backupId, "--json", "--mode=blobs-per-file" }, TimeSpan.FromSeconds(10));
        
        // Parse stats (guard nulls to keep the request fast even when restic is slow)
        long totalSize = 0;
        int totalFileCount = 0;
        
        if (!string.IsNullOrWhiteSpace(statsOutput))
        {
            try
            {
                using var statsDoc = JsonDocument.Parse(statsOutput);
                var stats = statsDoc.RootElement;
                totalSize = stats.TryGetProperty("total_size", out var size) ? size.GetInt64() : 0;
                totalFileCount = stats.TryGetProperty("total_file_count", out var count) ? count.GetInt32() : 0;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse backup stats for {BackupId}", backupId);
            }
        }

        // Parse raw-data stats for actual stored size after deduplication
        long rawDataSize = 0;
        if (!string.IsNullOrWhiteSpace(rawDataOutput))
        {
            try
            {
                using var rawDoc = JsonDocument.Parse(rawDataOutput);
                var rawStats = rawDoc.RootElement;
                rawDataSize = rawStats.TryGetProperty("total_size", out var rds) ? rds.GetInt64() : 0;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse raw-data stats for {BackupId}", backupId);
            }
        }

        // Parse blobs stats
        long totalBlobCount = 0;
        if (!string.IsNullOrWhiteSpace(blobsOutput))
        {
            try
            {
                using var blobDoc = JsonDocument.Parse(blobsOutput);
                var blobStats = blobDoc.RootElement;
                totalBlobCount = blobStats.TryGetProperty("total_blob_count", out var tbc) ? tbc.GetInt64() : 0;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Failed to parse blob stats for {BackupId}", backupId);
            }
        }

        var logicalSize = totalSize;
        var dedupedSize = rawDataSize > 0 ? rawDataSize : logicalSize;
        var spaceSaved = Math.Max(0, logicalSize - dedupedSize);
        var deduplicationRatio = logicalSize > 0
            ? Math.Min(1.0, Math.Max(0.0, (double)spaceSaved / logicalSize))
            : 0.0;

        var backup = new Backup
        {
            Id = metadata.Id,
            DeviceId = Guid.Empty,
            ShareId = null,
            DeviceName = metadata.Hostname,
            ShareName = null,
            Timestamp = metadata.Time,
            Status = BackupStatus.Success,
            FilesNew = metadata.Summary?.FilesNew ?? 0,
            FilesChanged = metadata.Summary?.FilesChanged ?? 0,
            FilesUnmodified = metadata.Summary?.FilesUnmodified ?? totalFileCount,
            DataAdded = metadata.Summary?.DataAdded ?? 0,
            DataProcessed = metadata.Summary?.DataProcessed ?? logicalSize,
            Duration = TimeSpan.Zero
        };

        var snapshotStats = new SnapshotStats
        {
            SnapshotId = backupId,
            TotalBlobCount = totalBlobCount,
            TotalTreeCount = 0,
            TotalSize = dedupedSize,
            TotalUncompressedSize = logicalSize,
            CompressionRatio = 0.0,
            DeduplicationSpaceSaved = spaceSaved,
            DeduplicationRatio = deduplicationRatio
        };

        return (backup, metadata, snapshotStats);
    }
    public async Task<(int SnapshotsPruned, long SpaceReclaimedBytes)> ApplyRetentionPolicy(string deviceName, string? shareName, RetentionPolicy policy, bool dryRun = false, string? repositoryPath = null)
    {
        _logger.LogInformation("Applying retention policy for {Device}/{Share}: keep-latest={Latest}, keep-daily={Daily}, keep-weekly={Weekly}, keep-monthly={Monthly}, keep-yearly={Yearly}, dry-run={DryRun}",
            deviceName, shareName ?? "(all)", policy.KeepLatest, policy.KeepDaily, policy.KeepWeekly, policy.KeepMonthly, policy.KeepYearly, dryRun);

        var args = new List<string> { "forget" };

        // Add retention policy arguments
        if (policy.KeepLatest > 0)
            args.AddRange(new[] { "--keep-last", policy.KeepLatest.ToString() });
        if (policy.KeepDaily > 0)
            args.AddRange(new[] { "--keep-daily", policy.KeepDaily.ToString() });
        if (policy.KeepWeekly > 0)
            args.AddRange(new[] { "--keep-weekly", policy.KeepWeekly.ToString() });
        if (policy.KeepMonthly > 0)
            args.AddRange(new[] { "--keep-monthly", policy.KeepMonthly.ToString() });
        if (policy.KeepYearly > 0)
            args.AddRange(new[] { "--keep-yearly", policy.KeepYearly.ToString() });

        // Add tags to filter snapshots
        args.Add("--tag");
        args.Add($"device:{deviceName}");
        if (shareName != null)
        {
            args.Add("--tag");
            args.Add($"share:{shareName}");
        }

        if (dryRun)
        {
            args.Add("--dry-run");
        }

        // Run forget to mark snapshots for deletion
        var forgetOutput = await _client.ExecuteCommand(args.ToArray(), repositoryPathOverride: repositoryPath);
        
        // Parse forget output to count removed snapshots
        // Restic forget outputs lines like "remove 5 snapshots" or "5 snapshots have been removed"
        int snapshotsPruned = 0;
        var snapshotPattern = new System.Text.RegularExpressions.Regex(
            @"(?:remove|removed|removing)\s+(\d+)\s+snapshot|(?<!keep\s+)(\d+)\s+snapshot.*(?:remove|removed)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        foreach (var line in forgetOutput.Split('\n'))
        {
            var match = snapshotPattern.Match(line);
            if (match.Success)
            {
                var countStr = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (int.TryParse(countStr, out var count))
                {
                    snapshotsPruned += count;
                    _logger.LogDebug("Parsed snapshot count from line: {Line} -> {Count}", line.Trim(), count);
                }
            }
        }

        _logger.LogInformation("Forget completed: {Count} snapshots marked for removal", snapshotsPruned);

        long spaceReclaimedBytes = 0;
        
        // Only run prune if not in dry-run mode
        if (!dryRun && snapshotsPruned > 0)
        {
            _logger.LogInformation("Running prune to reclaim space...");
            var pruneArgs = new[] { "prune" };
            var pruneOutput = await _client.ExecuteCommand(pruneArgs, repositoryPathOverride: repositoryPath);
            
            // Parse prune output for space reclaimed
            // Restic prune outputs lines like "freed 512.5 MiB" or "will free 2.3 GiB" or "2.3 GiB freed"
            var spacePattern = new System.Text.RegularExpressions.Regex(
                @"(?:freed?|will\s+free)\s+([\d.]+)\s*(\w+)|([\d.]+)\s*(\w+)\s+freed?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (var line in pruneOutput.Split('\n'))
            {
                var match = spacePattern.Match(line);
                if (match.Success)
                {
                    var sizeStr = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                    var unit = (match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value).ToUpperInvariant();
                    
                    if (double.TryParse(sizeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var size))
                    {
                        var bytesFromUnit = unit switch
                        {
                            "B" or "BYTES" => (long)size,
                            "K" or "KB" or "KIB" or "KIBIBYTE" or "KIBIBYTES" => (long)(size * 1024),
                            "M" or "MB" or "MIB" or "MEBIBYTE" or "MEBIBYTES" => (long)(size * 1024 * 1024),
                            "G" or "GB" or "GIB" or "GIBIBYTE" or "GIBIBYTES" => (long)(size * 1024 * 1024 * 1024),
                            "T" or "TB" or "TIB" or "TEBIBYTE" or "TEBIBYTES" => (long)(size * 1024L * 1024 * 1024 * 1024),
                            _ => 0
                        };
                        
                        if (bytesFromUnit > 0)
                        {
                            spaceReclaimedBytes = bytesFromUnit;
                            _logger.LogDebug("Parsed space reclaimed from line: {Line} -> {Bytes} bytes", line.Trim(), bytesFromUnit);
                            break;
                        }
                    }
                }
            }
            
            _logger.LogInformation("Prune completed: {Bytes} bytes reclaimed", spaceReclaimedBytes);
        }

        return (snapshotsPruned, spaceReclaimedBytes);
    }
    public async Task<IEnumerable<FileEntry>> BrowseBackup(string backupId, string path = "/", string? repositoryPath = null)
    {
        var args = new[] { "ls", backupId, "--json" };
        var output = await _client.ExecuteCommand(args, repositoryPathOverride: repositoryPath);
        
        _logger.LogInformation(
            "Restic ls output for backup {BackupId}: {LineCount} lines",
            backupId,
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        
        var files = new List<FileEntry>();
        
        try
        {
            // Parse each line as separate JSON object
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("struct_type", out var structType) && 
                        structType.GetString() == "node")
                    {
                        var filePath = root.GetProperty("path").GetString() ?? "";
                        var fileName = Path.GetFileName(filePath);
                        var nodeType = root.GetProperty("type").GetString();
                        var isDirectory = nodeType == "dir";
                        
                        // Filter by requested path
                        // Don't trim "/" from root path, otherwise it becomes empty string
                        var normalizedPath = path == "/" ? "/" : path.Replace("\\", "/").TrimEnd('/');
                        var normalizedFilePath = filePath.Replace("\\", "/");
                        
                        // Only include files directly in the requested path
                        var parentPath = Path.GetDirectoryName(normalizedFilePath)?.Replace("\\", "/") ?? "";
                        
                        // Debug logging for first few files
                        if (files.Count < 3)
                        {
                            _logger.LogDebug(
                                "File: {FilePath}, Parent: {ParentPath}, NormalizedPath: {NormalizedPath}, Match: {Match}",
                                filePath,
                                parentPath,
                                normalizedPath,
                                parentPath == normalizedPath);
                        }
                        
                        // Skip files not in the requested path
                        if (parentPath != normalizedPath)
                        {
                            continue;
                        }
                        
                        files.Add(new FileEntry
                        {
                            Name = fileName,
                            Path = filePath,
                            IsDirectory = isDirectory,
                            Size = root.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                            ModifiedAt = root.TryGetProperty("mtime", out var mtime) 
                                ? mtime.GetDateTime() 
                                : DateTime.MinValue,
                            Permissions = root.TryGetProperty("mode", out var mode) 
                                ? GetModeAsOctal(mode)
                                : null
                        });
                    }
                }
                catch (JsonException)
                {
                    // Skip non-JSON lines (headers, etc.)
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse backup {BackupId} at path {Path}", backupId, path);
            throw;
        }
        
        _logger.LogInformation(
            "Browse backup {BackupId} at path {Path}: returning {FileCount} files",
            backupId,
            path,
            files.Count);
        
        return files;
    }

    private static string? GetModeAsOctal(JsonElement mode)
    {
        try
        {
            // Try to get as different numeric types since mode can be large
            if (mode.ValueKind == JsonValueKind.Number)
            {
                // Try Int64 first for large values
                if (mode.TryGetInt64(out var longValue))
                {
                    return Convert.ToString(longValue, 8);
                }
                
                // Fall back to Int32 for smaller values
                if (mode.TryGetInt32(out var intValue))
                {
                    return Convert.ToString(intValue, 8);
                }
            }
            else if (mode.ValueKind == JsonValueKind.String)
            {
                // Handle mode as string (some restic versions might do this)
                var modeStr = mode.GetString();
                if (long.TryParse(modeStr, out var parsedValue))
                {
                    return Convert.ToString(parsedValue, 8);
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public Task<IEnumerable<FileVersion>> GetFileHistory(string deviceName, string filePath)
    {
        // TODO: Implement file history across backups
        return Task.FromResult(Enumerable.Empty<FileVersion>());
    }

    public async Task ApplyRetentionPolicy(string deviceName, RetentionPolicy policy)
    {
        var args = new List<string>
        {
            "forget",
            "--host", deviceName,
            "--keep-last", policy.KeepLatest.ToString(),
            "--keep-daily", policy.KeepDaily.ToString(),
            "--keep-weekly", policy.KeepWeekly.ToString(),
            "--keep-monthly", policy.KeepMonthly.ToString(),
            "--keep-yearly", policy.KeepYearly.ToString(),
            "--prune"
        };

        await _client.ExecuteCommand(args.ToArray());
    }

    public async Task<byte[]> DumpFile(string backupId, string filePath, string? repositoryPath = null)
    {
        try
        {
            _logger.LogInformation("Dumping file {FilePath} from backup {BackupId}", filePath, backupId);
            
            var args = new[] { "dump", backupId, filePath };
            var bytes = await _client.ExecuteCommandBinary(args, repositoryPathOverride: repositoryPath);
            
            _logger.LogInformation("Successfully dumped file {FilePath} from backup {BackupId}, size: {Size} bytes", 
                filePath, backupId, bytes.Length);
            
            return bytes;
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("unable to find snapshot") || 
            ex.Message.Contains("unable to load snapshot") ||
            ex.Message.Contains("snapshot") && ex.Message.Contains("not found") ||
            ex.Message.Contains("repository does not exist"))
        {
            _logger.LogError("Backup {BackupId} not found for file dump", backupId);
            throw new KeyNotFoundException($"Backup {backupId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dump file {FilePath} from backup {BackupId}", filePath, backupId);
            throw;
        }
    }

    public async Task<Stream> DumpFileStream(string backupId, string filePath, string? repositoryPath = null)
    {
        try
        {
            _logger.LogInformation("Streaming file {FilePath} from backup {BackupId}", filePath, backupId);
            
            var args = new[] { "dump", backupId, filePath };
            var stream = await _client.ExecuteCommandStream(args, repositoryPathOverride: repositoryPath);
            
            _logger.LogInformation("Successfully started streaming file {FilePath} from backup {BackupId}", 
                filePath, backupId);
            
            return stream;
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("unable to find snapshot") || 
            ex.Message.Contains("unable to load snapshot") ||
            ex.Message.Contains("snapshot") && ex.Message.Contains("not found") ||
            ex.Message.Contains("repository does not exist"))
        {
            _logger.LogError("Backup {BackupId} not found for file dump", backupId);
            throw new KeyNotFoundException($"Backup {backupId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream file {FilePath} from backup {BackupId}", filePath, backupId);
            throw;
        }
    }

    public async Task RestoreBackup(string backupId, string targetPath, string[]? includePaths = null, string? repositoryPath = null)
    {
        try
        {
            _logger.LogInformation("Starting restore of backup {BackupId} to {TargetPath}", backupId, targetPath);
            
            var args = new List<string> { "restore", backupId, "--target", targetPath };

            // If specific paths are requested, include only those
            if (includePaths != null && includePaths.Length > 0)
            {
                foreach (var path in includePaths)
                {
                    args.Add("--include");
                    args.Add(path);
                }
            }

            var output = await _client.ExecuteCommand(args.ToArray(), repositoryPathOverride: repositoryPath);
            _logger.LogInformation("Successfully restored backup {BackupId} to {TargetPath}", backupId, targetPath);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("unable to find snapshot") || 
            ex.Message.Contains("unable to load snapshot") ||
            ex.Message.Contains("snapshot") && ex.Message.Contains("not found") ||
            ex.Message.Contains("no snapshot found") ||
            ex.Message.Contains("snapshot does not exist") ||
            ex.Message.Contains("Is there a repository"))
        {
            _logger.LogWarning(ex, "Backup {BackupId} not found", backupId);
            throw new KeyNotFoundException($"Backup {backupId} not found", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup {BackupId} to {TargetPath}", backupId, targetPath);
            throw;
        }
    }

    public Task<RestoreProgress> GetRestoreProgress(string restoreId)
    {
        // TODO: Implement restore progress tracking
        return Task.FromResult(new RestoreProgress
        {
            RestoreId = restoreId,
            FilesRestored = 0,
            BytesRestored = 0,
            PercentComplete = 0
        });
    }

    private static SnapshotMetadata ParseSnapshotMetadata(JsonElement snapshot, string backupId)
    {
        var metadata = new SnapshotMetadata
        {
            Id = snapshot.GetProperty("short_id").GetString() ?? backupId,
            Parent = snapshot.TryGetProperty("parent", out var parent) && parent.ValueKind != JsonValueKind.Null
                ? parent.GetString()
                : null,
            Hostname = snapshot.GetProperty("hostname").GetString() ?? "unknown",
            Paths = snapshot.GetProperty("paths").EnumerateArray().Select(p => p.GetString() ?? "").ToList(),
            Time = snapshot.GetProperty("time").GetDateTime(),
            Tags = snapshot.TryGetProperty("tags", out var tags)
                ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToList()
                : new List<string>()
        };

        // Try to get summary from snapshot
        if (snapshot.TryGetProperty("summary", out var summaryElem))
        {
            metadata.Summary = new SnapshotSummary
            {
                FilesNew = summaryElem.TryGetProperty("files_new", out var fn) ? fn.GetInt32() : 0,
                FilesChanged = summaryElem.TryGetProperty("files_changed", out var fc) ? fc.GetInt32() : 0,
                FilesUnmodified = summaryElem.TryGetProperty("files_unmodified", out var fu) ? fu.GetInt32() : 0,
                DirsNew = summaryElem.TryGetProperty("dirs_new", out var dn) ? dn.GetInt32() : 0,
                DirsChanged = summaryElem.TryGetProperty("dirs_changed", out var dc) ? dc.GetInt32() : 0,
                DirsUnmodified = summaryElem.TryGetProperty("dirs_unmodified", out var du) ? du.GetInt32() : 0,
                DataAdded = summaryElem.TryGetProperty("data_added", out var da) ? da.GetInt64() : 0,
                DataProcessed = summaryElem.TryGetProperty("total_bytes_processed", out var tbp) ? tbp.GetInt64() : 0,
                TotalFilesProcessed = summaryElem.TryGetProperty("total_files_processed", out var tfp) ? tfp.GetInt32() : 0,
                TotalBytesProcessed = summaryElem.TryGetProperty("total_bytes_processed", out var _) ? tbp.GetInt64() : 0
            };
        }

        return metadata;
    }
}
