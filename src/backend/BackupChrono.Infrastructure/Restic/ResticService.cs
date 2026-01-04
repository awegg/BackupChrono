using System.Text.Json;
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
        catch (Exception ex) when (ex.Message.Contains("already initialized"))
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

    public async Task<Backup> CreateBackup(string repositoryPath, Device device, Share? share, string sourcePath, IncludeExcludeRules rules, Action<BackupProgress>? onProgress = null, CancellationToken cancellationToken = default)
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
        }, repositoryPathOverride: repositoryPath);
        
        // TODO: Parse backup result from JSON
        return new Backup
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            DeviceId = device.Id,
            ShareId = share?.Id,
            DeviceName = device.Name,
            ShareName = share?.Name,
            Timestamp = DateTime.UtcNow,
            Status = BackupStatus.Success,
            FilesNew = 0,
            FilesChanged = 0,
            FilesUnmodified = 0,
            DataAdded = 0,
            DataProcessed = 0,
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
        // Get snapshot details
        var args = new[] { "snapshots", backupId, "--json" };
        var output = await _client.ExecuteCommand(args, repositoryPathOverride: repositoryPath);
        
        try
        {
            using var doc = JsonDocument.Parse(output);
            var snapshots = doc.RootElement;
            
            if (snapshots.GetArrayLength() == 0)
            {
                throw new KeyNotFoundException($"Backup {backupId} not found");
            }
            
            var snapshot = snapshots[0];
            var id = snapshot.GetProperty("short_id").GetString() ?? string.Empty;
            var hostname = snapshot.GetProperty("hostname").GetString() ?? "unknown";
            var timestamp = snapshot.GetProperty("time").GetDateTime();
            
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
                Id = id,
                DeviceId = Guid.Empty,
                ShareId = null,
                DeviceName = hostname,
                ShareName = null,
                Timestamp = timestamp,
                Status = BackupStatus.Success,
                FilesNew = 0,
                FilesChanged = 0,
                FilesUnmodified = totalFileCount,
                DataAdded = 0,
                DataProcessed = totalSize,
                Duration = TimeSpan.Zero
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse backup {BackupId} from restic output", backupId);
            throw;
        }
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
                            _logger.LogInformation(
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
}
