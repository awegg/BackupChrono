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
    public Task SaveLog(BackupExecutionLog log)
    {
        try
        {
            var json = JsonSerializer.Serialize(log, _jsonOptions);
            
            lock (_fileLock)
            {
                // Append to JSONL file (one JSON object per line)
                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }
            
            _logger.LogDebug("Persisted execution log for backup {BackupId}", log.BackupId);
            return Task.CompletedTask;
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
    public Task<Dictionary<string, BackupExecutionLog>> LoadLogs()
    {
        var logs = new Dictionary<string, BackupExecutionLog>();

        if (!File.Exists(_logFilePath))
        {
            _logger.LogInformation("No existing execution logs found at {LogFilePath}", _logFilePath);
            return Task.FromResult(logs);
        }

        try
        {
            string[] lines;
            lock (_fileLock)
            {
                lines = File.ReadAllLines(_logFilePath);
            }

            foreach (var line in lines)
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
            return Task.FromResult(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load execution logs from {LogFilePath}", _logFilePath);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific log by backup ID (loads all logs and filters).
    /// For performance-critical scenarios, use LoadLogs() once and cache.
    /// </summary>
    public async Task<BackupExecutionLog?> GetLog(string backupId)
    {
        var logs = await LoadLogs();
        logs.TryGetValue(backupId, out var log);
        return log;
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
