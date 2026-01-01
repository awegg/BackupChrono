using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Infrastructure.Restic;

/// <summary>
/// Implementation of IResticService for backup operations using restic.
/// </summary>
public class ResticService : IResticService
{
    private readonly ResticClient _client;

    public ResticService(ResticClient client)
    {
        _client = client;
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

    public async Task<Backup> CreateBackup(Device device, Share? share, string sourcePath, IncludeExcludeRules rules)
    {
        var args = new List<string> { "backup", sourcePath, "--json" };

        // Add exclude patterns
        foreach (var pattern in rules.ExcludePatterns)
        {
            args.Add("--exclude");
            args.Add(pattern);
        }

        var output = await _client.ExecuteCommand(args.ToArray());
        
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
            BytesTransferred = 0,
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
        throw new NotImplementedException();
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
