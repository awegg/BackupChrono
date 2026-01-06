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
    private readonly Lazy<Task> _initTask;

    public InMemoryBackupLogService(BackupExecutionLogRepository repository, ILogger<InMemoryBackupLogService> logger)
    {
        _repository = repository;
        _logger = logger;

        // Use lazy initialization to avoid blocking constructor
        _initTask = new Lazy<Task>(async () =>
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
        });
    }

    private async Task EnsureInitializedAsync()
    {
        await _initTask.Value;
    }

    public async Task<BackupExecutionLog> GetOrCreateLog(string backupId, string? jobId = null)
    {
        await EnsureInitializedAsync();
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
            return log;
        }
    }

    public async Task<BackupExecutionLog?> GetLog(string backupId)
    {
        await EnsureInitializedAsync();
        lock (_lock)
        {
            _logs.TryGetValue(backupId, out var log);
            return log;
        }
    }

    public async Task AddWarning(string backupId, string warning)
    {
        await EnsureInitializedAsync();
        lock (_lock)
        {
            if (!_logs.TryGetValue(backupId, out var log))
            {
                log = new BackupExecutionLog { BackupId = backupId, CreatedAt = DateTime.UtcNow };
                _logs[backupId] = log;
            }
            log.Warnings.Add(warning);
            log.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task AddError(string backupId, string error)
    {
        await EnsureInitializedAsync();
        lock (_lock)
        {
            if (!_logs.TryGetValue(backupId, out var log))
            {
                log = new BackupExecutionLog { BackupId = backupId, CreatedAt = DateTime.UtcNow };
                _logs[backupId] = log;
            }
            log.Errors.Add(error);
            log.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task AddProgressEntry(string backupId, ProgressLogEntry entry)
    {
        await EnsureInitializedAsync();
        lock (_lock)
        {
            if (!_logs.TryGetValue(backupId, out var log))
            {
                log = new BackupExecutionLog { BackupId = backupId, CreatedAt = DateTime.UtcNow };
                _logs[backupId] = log;
            }
            log.ProgressLog.Add(entry);
            log.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task PersistLog(string backupId)
    {
        await EnsureInitializedAsync();
        BackupExecutionLog snapshot;
        lock (_lock)
        {
            if (!_logs.TryGetValue(backupId, out var log))
            {
                _logger.LogWarning("Attempted to persist non-existent log for backup {BackupId}", backupId);
                return;
            }
            // Create snapshot to avoid concurrent modifications during persistence
            snapshot = new BackupExecutionLog
            {
                BackupId = log.BackupId,
                JobId = log.JobId,
                Warnings = new List<string>(log.Warnings),
                Errors = new List<string>(log.Errors),
                ProgressLog = new List<ProgressLogEntry>(log.ProgressLog),
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt
            };
        }

        await _repository.SaveLog(snapshot);
        _logger.LogInformation("Persisted execution log for backup {BackupId} ({Warnings} warnings, {Errors} errors, {Progress} progress entries)",
            backupId, snapshot.Warnings.Count, snapshot.Errors.Count, snapshot.ProgressLog.Count);
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
