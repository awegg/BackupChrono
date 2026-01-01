using BackupChrono.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Monitors storage capacity and provides alerts based on configurable thresholds.
/// </summary>
public class StorageMonitor : IStorageMonitor
{
    private readonly ILogger<StorageMonitor> _logger;
    private readonly StorageMonitorOptions _options;
    private readonly string _repositoryBasePath;

    public StorageMonitor(
        ILogger<StorageMonitor> logger,
        Restic.ResticClient resticClient,
        StorageMonitorOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new StorageMonitorOptions();
        _repositoryBasePath = resticClient?.RepositoryPath ?? string.Empty;
    }

    public Task<StorageStatus> GetStorageStatus(string path)
    {
        try
        {
            // Get the drive info for the given path
            var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? path);

            if (!driveInfo.IsReady)
            {
                _logger.LogWarning("Drive for path {Path} is not ready", path);
                return Task.FromResult(new StorageStatus
                {
                    Path = path,
                    ThresholdLevel = StorageThresholdLevel.Critical,
                    Message = "Drive not ready"
                });
            }

            var totalBytes = driveInfo.TotalSize;
            var availableBytes = driveInfo.AvailableFreeSpace;
            var usedBytes = totalBytes - availableBytes;
            var usedPercentage = (double)usedBytes / totalBytes * 100;

            var thresholdLevel = DetermineThresholdLevel(usedPercentage);

            var status = new StorageStatus
            {
                Path = path,
                TotalBytes = totalBytes,
                AvailableBytes = availableBytes,
                UsedBytes = usedBytes,
                UsedPercentage = usedPercentage,
                ThresholdLevel = thresholdLevel,
                Message = GetThresholdMessage(thresholdLevel, availableBytes, usedPercentage)
            };

            // Log warnings for critical states
            if (thresholdLevel >= StorageThresholdLevel.Critical)
            {
                _logger.LogWarning(
                    "Storage at {Path} is {Level}: {Used:F1}% used ({Available:N0} bytes available)",
                    path, thresholdLevel, usedPercentage, availableBytes);
            }

            return Task.FromResult(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get storage status for path {Path}", path);
            return Task.FromResult(new StorageStatus
            {
                Path = path,
                ThresholdLevel = StorageThresholdLevel.Critical,
                Message = $"Error checking storage: {ex.Message}"
            });
        }
    }

    public async Task<bool> HasSufficientSpace(string path, long estimatedSizeBytes)
    {
        var status = await GetStorageStatus(path);

        // Don't allow backups if storage is exhausted
        if (status.ThresholdLevel == StorageThresholdLevel.Exhausted)
        {
            _logger.LogError(
                "Storage exhausted at {Path}. Cannot proceed with backup requiring {Size:N0} bytes",
                path, estimatedSizeBytes);
            return false;
        }

        // Check if there's enough space for the estimated size
        var requiredSpace = estimatedSizeBytes + _options.MinimumFreeSpaceBytes;
        if (status.AvailableBytes < requiredSpace)
        {
            _logger.LogWarning(
                "Insufficient space at {Path}. Available: {Available:N0} bytes, Required: {Required:N0} bytes",
                path, status.AvailableBytes, requiredSpace);
            return false;
        }

        return true;
    }

    public async Task<IEnumerable<StorageStatus>> GetAllRepositoryStorageStatus()
    {
        if (string.IsNullOrWhiteSpace(_repositoryBasePath))
        {
            _logger.LogInformation("Repository path is not configured; skipping storage check");
            return Enumerable.Empty<StorageStatus>();
        }

        var repositoryPath = _repositoryBasePath;

        if (!Directory.Exists(repositoryPath))
        {
            _logger.LogInformation("Repository directory does not exist yet: {Path}", repositoryPath);
            return Enumerable.Empty<StorageStatus>();
        }

        var status = await GetStorageStatus(repositoryPath);
        return new[] { status };
    }

    private StorageThresholdLevel DetermineThresholdLevel(double usedPercentage)
    {
        if (usedPercentage >= _options.ExhaustedThresholdPercent)
            return StorageThresholdLevel.Exhausted;
        
        if (usedPercentage >= _options.CriticalThresholdPercent)
            return StorageThresholdLevel.Critical;
        
        if (usedPercentage >= _options.WarningThresholdPercent)
            return StorageThresholdLevel.Warning;
        
        return StorageThresholdLevel.Normal;
    }

    private string GetThresholdMessage(StorageThresholdLevel level, long availableBytes, double usedPercentage)
    {
        var availableGB = availableBytes / 1024.0 / 1024.0 / 1024.0;

        return level switch
        {
            StorageThresholdLevel.Exhausted => $"Storage exhausted: {usedPercentage:F1}% used, {availableGB:F2} GB available",
            StorageThresholdLevel.Critical => $"Storage critical: {usedPercentage:F1}% used, {availableGB:F2} GB available",
            StorageThresholdLevel.Warning => $"Storage warning: {usedPercentage:F1}% used, {availableGB:F2} GB available",
            _ => $"Storage healthy: {usedPercentage:F1}% used, {availableGB:F2} GB available"
        };
    }
}

/// <summary>
/// Configuration options for storage monitoring.
/// </summary>
public class StorageMonitorOptions
{
    /// <summary>
    /// Percentage used that triggers warning level (default: 80%).
    /// </summary>
    public double WarningThresholdPercent { get; set; } = 80.0;

    /// <summary>
    /// Percentage used that triggers critical level (default: 90%).
    /// </summary>
    public double CriticalThresholdPercent { get; set; } = 90.0;

    /// <summary>
    /// Percentage used that triggers exhausted level (default: 95%).
    /// </summary>
    public double ExhaustedThresholdPercent { get; set; } = 95.0;

    /// <summary>
    /// Minimum free space required in bytes (default: 1 GB).
    /// </summary>
    public long MinimumFreeSpaceBytes { get; set; } = 1024L * 1024 * 1024; // 1 GB
}
