using BackupChrono.Core.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackupChrono.Infrastructure.Repositories;

/// <summary>
/// JSONL-based repository for persisting backup execution logs.
/// Follows append-only pattern for durability.
/// </summary>
public class BackupExecutionLogRepository
{
    private readonly string _logFilePath;
    private readonly ILogger<BackupExecutionLogRepository> _logger;
    private readonly object _fileLock = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public BackupExecutionLogRepository(string logsDirectory, ILogger<BackupExecutionLogRepository> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, "backup-execution-logs.jsonl");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        
        _logger.LogInformation("BackupExecutionLogRepository initialized with path: {LogFilePath}", _logFilePath);
    }

    /// <summary>
    /// Persists a backup execution log to JSONL file (append-only).
    /// </summary>
    public async Task SaveLog(BackupExecutionLog log)
    {
        try
        {
            var json = JsonSerializer.Serialize(log, _jsonOptions);
            
            // Use semaphore for async-safe file access
            using var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
            using var writer = new StreamWriter(fileStream);
            await writer.WriteLineAsync(json);
            
            _logger.LogDebug("Persisted execution log for backup {BackupId}", log.BackupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist execution log for backup {BackupId}", log.BackupId);
            throw;
        }
    }

    /// <summary>
    /// Loads all backup execution logs from JSONL file.
    /// Returns dictionary keyed by BackupId for quick lookup.
    /// </summary>
    public async Task<Dictionary<string, BackupExecutionLog>> LoadLogs()
    {
        var logs = new Dictionary<string, BackupExecutionLog>();

        if (!File.Exists(_logFilePath))
        {
            _logger.LogInformation("No existing execution logs found at {LogFilePath}", _logFilePath);
            return logs;
        }

        try
        {
            using var fileStream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            using var reader = new StreamReader(fileStream);
            
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var log = JsonSerializer.Deserialize<BackupExecutionLog>(line, _jsonOptions);
                    if (log != null)
                    {
                        // Later entries overwrite earlier ones (handles updates)
                        logs[log.BackupId] = log;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse execution log line: {Line}", line);
                }
            }

            _logger.LogInformation("Loaded {Count} execution logs from {LogFilePath}", logs.Count, _logFilePath);
            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load execution logs from {LogFilePath}", _logFilePath);
            throw;
        }
    }

    /// <summary>
    /// Clears all execution logs (for testing/maintenance).
    /// </summary>
    public void Clear()
    {
        lock (_fileLock)
        {
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
                _logger.LogInformation("Cleared all execution logs from {LogFilePath}", _logFilePath);
            }
        }
    }
}
