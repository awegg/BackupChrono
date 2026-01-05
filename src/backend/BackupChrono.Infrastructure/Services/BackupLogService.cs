using BackupChrono.Core.Entities;

namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Interface for storing and retrieving backup execution logs.
/// </summary>
public interface IBackupLogService
{
    /// <summary>
    /// Gets or creates a log for a backup.
    /// </summary>
    Task<BackupExecutionLog> GetOrCreateLog(string backupId, string? jobId = null);

    /// <summary>
    /// Gets an existing log by backup ID.
    /// </summary>
    Task<BackupExecutionLog?> GetLog(string backupId);

    /// <summary>
    /// Adds a warning to the log.
    /// </summary>
    Task AddWarning(string backupId, string warning);

    /// <summary>
    /// Adds an error to the log.
    /// </summary>
    Task AddError(string backupId, string error);

    /// <summary>
    /// Adds a progress entry to the log.
    /// </summary>
    Task AddProgressEntry(string backupId, ProgressLogEntry entry);

    /// <summary>
    /// Clears all logs (for testing).
    /// </summary>
    Task Clear();
}

/// <summary>
/// In-memory implementation of backup log storage.
/// </summary>
public class InMemoryBackupLogService : IBackupLogService
{
    private readonly Dictionary<string, BackupExecutionLog> _logs = new();
    private readonly object _lock = new();

    public Task<BackupExecutionLog> GetOrCreateLog(string backupId, string? jobId = null)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(backupId, out var log))
            {
                log = new BackupExecutionLog
                {
                    BackupId = backupId,
                    JobId = jobId
                };
                _logs[backupId] = log;
            }
            return Task.FromResult(log);
        }
    }

    public Task<BackupExecutionLog?> GetLog(string backupId)
    {
        lock (_lock)
        {
            _logs.TryGetValue(backupId, out var log);
            return Task.FromResult(log);
        }
    }

    public async Task AddWarning(string backupId, string warning)
    {
        var log = await GetOrCreateLog(backupId);
        lock (_lock)
        {
            log.Warnings.Add(warning);
            log.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task AddError(string backupId, string error)
    {
        var log = await GetOrCreateLog(backupId);
        lock (_lock)
        {
            log.Errors.Add(error);
            log.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task AddProgressEntry(string backupId, ProgressLogEntry entry)
    {
        var log = await GetOrCreateLog(backupId);
        lock (_lock)
        {
            log.ProgressLog.Add(entry);
            log.UpdatedAt = DateTime.UtcNow;
        }
    }

    public Task Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
        return Task.CompletedTask;
    }
}
