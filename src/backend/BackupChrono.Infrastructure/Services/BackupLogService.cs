using BackupChrono.Core.Entities;
using BackupChrono.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

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
    /// Persists a log to durable storage when backup completes/fails.
    /// </summary>
    Task PersistLog(string backupId);

    /// <summary>
    /// Clears all logs (for testing).
    /// </summary>
    Task Clear();
}

/// <summary>
/// Hybrid implementation: in-memory for active backups, JSONL persistence on completion.
/// </summary>
public class InMemoryBackupLogService : IBackupLogService
{
    private readonly Dictionary<string, BackupExecutionLog> _logs = new();
    private readonly object _lock = new();
    private readonly BackupExecutionLogRepository _repository;
    private readonly ILogger<InMemoryBackupLogService> _logger;

    public InMemoryBackupLogService(BackupExecutionLogRepository repository, ILogger<InMemoryBackupLogService> logger)
    {
        _repository = repository;
        _logger = logger;

        // Load existing logs on initialization
        Task.Run(async () =>
        {
            try
            {
                var persistedLogs = await _repository.LoadLogs();
                lock (_lock)
                {
                    foreach (var kvp in persistedLogs)
                    {
                        _logs[kvp.Key] = kvp.Value;
                    }
                }
                _logger.LogInformation("Loaded {Count} persisted execution logs on initialization", persistedLogs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load persisted execution logs on initialization");
            }
        }).Wait();
    }

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

    public async Task PersistLog(string backupId)
    {
        BackupExecutionLog? log;
        lock (_lock)
        {
            if (!_logs.TryGetValue(backupId, out log))
            {
                _logger.LogWarning("Attempted to persist non-existent log for backup {BackupId}", backupId);
                return;
            }
        }

        await _repository.SaveLog(log);
        _logger.LogInformation("Persisted execution log for backup {BackupId} ({Warnings} warnings, {Errors} errors, {Progress} progress entries)",
            backupId, log.Warnings.Count, log.Errors.Count, log.ProgressLog.Count);
    }

    public async Task Clear()
    {
        lock (_lock)
        {
            _logs.Clear();
        }
        _repository.Clear();
        await Task.CompletedTask;
    }
}
