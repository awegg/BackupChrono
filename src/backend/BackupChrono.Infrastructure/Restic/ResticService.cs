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
    private readonly ResticClient _client;
    private readonly ILogger<ResticService> _logger;
    private DateTime _lastLogTime = DateTime.MinValue;
    private static readonly TimeSpan LogThrottleInterval = TimeSpan.FromSeconds(1);

    public ResticService(ResticClient client, ILogger<ResticService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> InitializeRepository(string repositoryPath, string password)
    {
        try
        {
            await _client.ExecuteCommand(new[] { "init" });
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
            await _client.ExecuteCommand(new[] { "snapshots", "--json" });
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
        var output = await _client.ExecuteCommand(new[] { "stats", "--json" });
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
        await _client.ExecuteCommand(new[] { "check" });
    }

    public async Task Prune(string repositoryPath)
    {
        await _client.ExecuteCommand(new[] { "prune" });
    }

    public async Task<Backup> CreateBackup(Device device, Share? share, string sourcePath, IncludeExcludeRules rules, Action<BackupProgress>? onProgress = null)
    {
        var args = new List<string> { "backup", sourcePath, "--json" };

        // Add exclude patterns
        foreach (var pattern in rules.ExcludePatterns)
        {
            args.Add("--exclude");
            args.Add(pattern);
        }

        var output = await _client.ExecuteCommand(args.ToArray(), onOutputLine: line =>
        {
            if (onProgress == null || string.IsNullOrWhiteSpace(line)) return;
            
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("message_type", out var messageType) && 
                    messageType.GetString() == "status")
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
                    if (now - _lastLogTime >= LogThrottleInterval)
                    {
                        _logger.LogDebug("Restic progress: {Percent}% - {Files}/{TotalFiles} files", 
                            progress.PercentComplete, progress.FilesProcessed, progress.TotalFiles);
                        _lastLogTime = now;
                    }
                    
                    onProgress(progress);
                }
            }
            catch (JsonException)
            {
                // Ignore JSON parse errors for non-JSON output lines (restic outputs mixed text/JSON)
            }
        });
        
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

    public async Task<IEnumerable<Backup>> ListBackups(string? deviceName = null)
    {
        var args = new List<string> { "snapshots", "--json" };
        
        if (deviceName != null)
        {
            args.Add("--host");
            args.Add(deviceName);
        }

        var output = await _client.ExecuteCommand(args.ToArray());
        
        // TODO: Parse snapshots from JSON
        return Enumerable.Empty<Backup>();
    }

    public Task<Backup> GetBackup(string backupId)
    {
        // TODO: Implement get backup by ID
        return Task.FromException<Backup>(new NotImplementedException("GetBackup is not yet implemented"));
    }

    public async Task<IEnumerable<FileEntry>> BrowseBackup(string backupId, string path = "/")
    {
        var args = new[] { "ls", backupId, path, "--json" };
        var output = await _client.ExecuteCommand(args);
        
        // TODO: Parse file list from JSON
        return Enumerable.Empty<FileEntry>();
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

    public async Task RestoreBackup(string backupId, string targetPath, string[]? includePaths = null)
    {
        var args = new List<string> { "restore", backupId, "--target", targetPath };

        if (includePaths != null)
        {
            foreach (var path in includePaths)
            {
                args.Add("--include");
                args.Add(path);
            }
        }

        await _client.ExecuteCommand(args.ToArray());
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
