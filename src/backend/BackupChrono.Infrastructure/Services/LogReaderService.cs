using BackupChrono.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BackupChrono.Infrastructure.Services;

public interface ILogReaderService
{
    Task<IEnumerable<LogEntry>> GetLogsAsync(LogQueryParameters parameters, CancellationToken cancellationToken = default);
}

public class LogReaderService : ILogReaderService
{
    private readonly string _logDirectory;
    private readonly ILogger<LogReaderService> _logger;
    
    // Regex to parse Serilog log format: 2026-01-11 21:10:52.832 +01:00 [LEVEL] Message
    private static readonly Regex LogLineRegex = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}\s+[+-]\d{2}:\d{2})\s+\[(?<level>\w{3})\]\s+(?<message>.*)$",
        RegexOptions.Compiled);

    public LogReaderService(IConfiguration configuration, ILogger<LogReaderService> logger)
    {
        _logger = logger;
        
        // Get the configured log path from Serilog settings
        var configuredPath = configuration["Serilog:WriteTo:1:Args:path"];
        
        if (!string.IsNullOrEmpty(configuredPath))
        {
            // The path is relative to the application's working directory (where dotnet run is executed)
            // For "logs/backupchrono-.log", we want just "logs"
            var logFileName = Path.GetFileName(configuredPath);
            var relativeDir = configuredPath.Replace(logFileName, "").TrimEnd('/', '\\');
            
            if (string.IsNullOrEmpty(relativeDir))
            {
                relativeDir = "logs";
            }
            
            // Use Directory.GetCurrentDirectory() which is the working directory, not the binary location
            _logDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativeDir));
        }
        else
        {
            // Fallback to logs directory in current working directory
            _logDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "logs"));
        }
        
        _logger.LogInformation("LogReaderService initialized with directory: {LogDirectory}", _logDirectory);
    }

    public async Task<IEnumerable<LogEntry>> GetLogsAsync(LogQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var logs = new List<LogEntry>();
        
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                _logger.LogWarning("Log directory does not exist: {LogDirectory}", _logDirectory);
                return logs;
            }

            // Get all log files, sorted by date (newest first)
            var logFiles = Directory.GetFiles(_logDirectory, "backupchrono-*.log")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            var limit = Math.Min(parameters.Limit, 1000); // Max 1000 entries
            
            foreach (var logFile in logFiles)
            {
                if (logs.Count >= limit)
                    break;

                await ReadLogFileAsync(logFile, logs, parameters, limit, cancellationToken);
            }

            return logs.Take(limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading log files from {LogDirectory}", _logDirectory);
            return logs;
        }
    }

    private async Task ReadLogFileAsync(
        string filePath, 
        List<LogEntry> logs, 
        LogQueryParameters parameters,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use FileShare.ReadWrite to allow reading while Serilog is writing
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line != null)
                {
                    lines.Add(line);
                }
            }
            
            var fileDate = ExtractDateFromFileName(filePath);
            LogEntry? currentEntry = null;

            foreach (var line in lines.AsEnumerable().Reverse()) // Read from bottom (newest) to top
            {
                if (logs.Count >= limit)
                    break;

                var match = LogLineRegex.Match(line);
                
                if (match.Success)
                {
                    // Save previous entry if exists
                    if (currentEntry != null && MatchesFilters(currentEntry, parameters))
                    {
                        logs.Add(currentEntry);
                    }

                    // Start new entry
                    var timestampStr = match.Groups["timestamp"].Value;
                    var timestamp = ParseTimestamp(fileDate, timestampStr);
                    
                    currentEntry = new LogEntry
                    {
                        Timestamp = timestamp,
                        Level = match.Groups["level"].Value,
                        Message = match.Groups["message"].Value.Trim()
                    };
                }
                else if (currentEntry != null)
                {
                    // Multi-line entry (exception or continuation)
                    if (string.IsNullOrEmpty(currentEntry.Exception))
                    {
                        currentEntry.Exception = line;
                    }
                    else
                    {
                        currentEntry.Exception = line + Environment.NewLine + currentEntry.Exception;
                    }
                }
            }

            // Add last entry
            if (currentEntry != null && MatchesFilters(currentEntry, parameters))
            {
                logs.Add(currentEntry);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read log file {FilePath}", filePath);
        }
    }

    private static DateTime ExtractDateFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var dateMatch = Regex.Match(fileName, @"backupchrono-(\d{8})");
        
        if (dateMatch.Success && DateTime.TryParseExact(
            dateMatch.Groups[1].Value, 
            "yyyyMMdd", 
            null, 
            System.Globalization.DateTimeStyles.None, 
            out var date))
        {
            return date;
        }

        return DateTime.Today;
    }

    private static DateTime ParseTimestamp(DateTime fileDate, string timestampStr)
    {
        // Parse full Serilog timestamp: 2026-01-11 21:10:52.832 +01:00
        if (DateTime.TryParse(timestampStr, out var timestamp))
        {
            return timestamp;
        }
        return fileDate;
    }

    private static bool MatchesFilters(LogEntry entry, LogQueryParameters parameters)
    {
        // Filter by level - normalize to 3-letter codes and support both full words and abbreviations
        if (!string.IsNullOrEmpty(parameters.Level))
        {
            var normalizedEntryLevel = NormalizeLevel(entry.Level);
            var normalizedFilterLevel = NormalizeLevel(parameters.Level);
            
            if (!normalizedEntryLevel.Equals(normalizedFilterLevel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Filter by date range
        if (parameters.StartDate.HasValue && entry.Timestamp < parameters.StartDate.Value)
        {
            return false;
        }

        if (parameters.EndDate.HasValue && entry.Timestamp > parameters.EndDate.Value)
        {
            return false;
        }

        // Filter by search text
        if (!string.IsNullOrEmpty(parameters.Search))
        {
            var searchLower = parameters.Search.ToLowerInvariant();
            var matchesMessage = entry.Message.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
            var matchesException = entry.Exception?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false;
            
            if (!matchesMessage && !matchesException)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Normalizes log level strings to 3-letter codes.
    /// Supports both 3-letter codes (ERR, WRN, INF, DBG) and full words (Error, Warning, Info, Debug).
    /// </summary>
    private static string NormalizeLevel(string level)
    {
        if (string.IsNullOrEmpty(level))
            return string.Empty;

        var upperLevel = level.ToUpperInvariant();
        
        // Map full words to 3-letter codes
        return upperLevel switch
        {
            "ERROR" => "ERR",
            "WARNING" => "WRN",
            "INFO" or "INFORMATION" => "INF",
            "DEBUG" => "DBG",
            _ => upperLevel.Length > 3 ? upperLevel.Substring(0, 3) : upperLevel
        };
    }
}
