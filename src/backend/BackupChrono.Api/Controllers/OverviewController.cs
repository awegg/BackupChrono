using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

/// <summary>
/// Controller for backup overview dashboard.
/// Provides aggregated data for monitoring all devices and shares.
/// </summary>
[ApiController]
[Route("api/overview")]
public class OverviewController : ControllerBase
{
    private readonly IBackupRepository _backupRepository;
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;
    private readonly ILogger<OverviewController> _logger;

    public OverviewController(
        IBackupRepository backupRepository,
        IDeviceService deviceService,
        IShareService shareService,
        ILogger<OverviewController> logger)
    {
        _backupRepository = backupRepository;
        _deviceService = deviceService;
        _shareService = shareService;
        _logger = logger;
    }

    /// <summary>
    /// Get backup overview dashboard data.
    /// Returns aggregated statistics and status for all devices and shares.
    /// </summary>
    /// <returns>Complete overview data including devices, shares, and summary metrics.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(BackupOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BackupOverviewDto>> GetOverview()
    {
        try
        {
            _logger.LogInformation("Fetching backup overview data");

            var devices = await _deviceService.ListDevices();
            var deviceOverviews = new List<DeviceOverviewDto>();

            int devicesNeedingAttention = 0;
            double totalProtectedDataGB = 0;
            int recentFailures = 0;
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);

            foreach (var device in devices)
            {
                var shares = await _shareService.ListShares(device.Id);
                var shareList = shares.ToList();
                var shareOverviews = new List<ShareOverviewDto>();

                double deviceTotalGB = 0;
                int deviceTotalFiles = 0;
                var deviceStatuses = new List<string>();

                foreach (var share in shareList)
                {
                    var lastBackup = await _backupRepository.GetLatestBackup(device.Id, share.Id);

                    // Calculate share status
                    string status = CalculateStatus(share, lastBackup, twoDaysAgo);
                    deviceStatuses.Add(status);

                    // Calculate metrics
                    var sizeGB = lastBackup != null 
                        ? lastBackup.DataAdded / (1024.0 * 1024 * 1024) 
                        : 0;
                    var fileCount = lastBackup != null
                        ? lastBackup.FilesNew +
                          lastBackup.FilesChanged +
                          lastBackup.FilesUnmodified
                        : 0;

                    deviceTotalGB += sizeGB;
                    deviceTotalFiles += fileCount;
                    totalProtectedDataGB += sizeGB;

                    // Count recent failures
                    if (status == "Failed" && lastBackup?.Timestamp > oneDayAgo)
                    {
                        recentFailures++;
                    }

                    shareOverviews.Add(new ShareOverviewDto
                    {
                        Id = share.Id,
                        Name = share.Name,
                        Path = share.Path,
                        LastBackupTimestamp = lastBackup?.Timestamp,
                        Status = status,
                        SizeGB = Math.Round(sizeGB, 2),
                        FileCount = fileCount,
                        IsStale = IsStale(lastBackup?.Timestamp, twoDaysAgo)
                    });
                }

                // Calculate device status (worst status wins)
                var deviceStatus = CalculateDeviceStatus(deviceStatuses);

                if (deviceStatus == "Warning" || deviceStatus == "Failed")
                {
                    devicesNeedingAttention++;
                }

                deviceOverviews.Add(new DeviceOverviewDto
                {
                    Id = device.Id,
                    Name = device.Name,
                    Status = deviceStatus,
                    SizeGB = Math.Round(deviceTotalGB, 2),
                    FileCount = deviceTotalFiles,
                    Shares = shareOverviews
                });
            }

            var result = new BackupOverviewDto
            {
                DevicesNeedingAttention = devicesNeedingAttention,
                TotalProtectedDataTB = Math.Round(totalProtectedDataGB / 1024, 2),
                RecentFailures = recentFailures,
                Devices = deviceOverviews
            };

            _logger.LogInformation(
                "Overview fetched: {DeviceCount} devices, {ShareCount} shares, {AttentionCount} needing attention",
                deviceOverviews.Count,
                deviceOverviews.Sum(d => d.Shares.Count),
                devicesNeedingAttention);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching backup overview");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to fetch backup overview",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Calculates the status for a share based on its configuration and last backup.
    /// </summary>
    private string CalculateStatus(Share share, Backup? lastBackup, DateTime twoDaysAgo)
    {
        // 1. Disabled shares always show as Disabled
        if (!share.Enabled)
        {
            return "Disabled";
        }

        // 2. No backup yet - show as Warning
        if (lastBackup == null)
        {
            return "Warning";
        }

        // 3. Map backup status to string
        var status = lastBackup.Status switch
        {
            BackupStatus.Success => "Success",
            BackupStatus.Failed => "Failed",
            BackupStatus.Partial => "Partial",
            _ => "Warning"
        };

        // 4. Check for stale successful backups (older than 2 days)
        if (status == "Success" && lastBackup.Timestamp < twoDaysAgo)
        {
            return "Warning"; // Stale backup
        }

        return status;
    }

    /// <summary>
    /// Calculates the overall device status based on all share statuses.
    /// Uses priority: Failed > Warning > Running > Partial > Disabled > Success
    /// </summary>
    private string CalculateDeviceStatus(List<string> shareStatuses)
    {
        if (shareStatuses.Count == 0)
        {
            return "Warning"; // No shares
        }

        // Priority order: Failed > Warning > Running > Partial > Disabled > Success
        if (shareStatuses.Contains("Failed")) return "Failed";
        if (shareStatuses.Contains("Warning")) return "Warning";
        if (shareStatuses.Contains("Running")) return "Running";
        if (shareStatuses.Contains("Partial")) return "Partial";
        if (shareStatuses.All(s => s == "Disabled")) return "Disabled";
        
        return "Success";
    }

    /// <summary>
    /// Determines if a backup is stale (older than 2 days or never backed up).
    /// </summary>
    private bool IsStale(DateTime? timestamp, DateTime twoDaysAgo)
    {
        return timestamp == null || timestamp < twoDaysAgo;
    }
}
