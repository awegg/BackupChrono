using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Quartz;

namespace BackupChrono.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IStorageMonitor _storageMonitor;
    private readonly ResticOptions _resticOptions;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDeviceService deviceService,
        IShareService shareService,
        IBackupJobRepository backupJobRepository,
        IStorageMonitor storageMonitor,
        IOptions<ResticOptions> resticOptions,
        ILogger<DashboardController> logger)
    {
        _deviceService = deviceService;
        _shareService = shareService;
        _backupJobRepository = backupJobRepository;
        _storageMonitor = storageMonitor;
        _resticOptions = resticOptions.Value;
        _logger = logger;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        try
        {
            var today = DateTime.UtcNow;
            var yesterday = today.AddHours(-24);

            // Load data
            var devices = await _deviceService.ListDevices();
            var allJobs = await _backupJobRepository.ListJobs();
            
            // Build response structure
            var response = new DashboardSummaryDto();
            
            // 1. Storage Stats
            try 
            {
                // We assume repository is at _resticOptions.RepositoryBasePath
                // GetStorageStatus returns usage of the volume containing that path
                var storageStatus = await _storageMonitor.GetStorageStatus(_resticOptions.RepositoryBasePath);
                response.Stats.TotalStoredBytes = storageStatus.UsedBytes; // This is volume usage, not just repo, but close enough for MVP
                
                if (storageStatus.ThresholdLevel == StorageThresholdLevel.Exhausted)
                {
                    response.Stats.SystemHealth = "Critical";
                }
                else if (storageStatus.ThresholdLevel >= StorageThresholdLevel.Warning)
                {
                    response.Stats.SystemHealth = "Warning";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get storage stats");
            }

            // 2. Aggregate Stats and Build Tree
            var allSharesCount = 0;
            var recentFailures = 0;
            var runningJobs = 0;

            foreach (var device in devices)
            {
                var deviceDto = new DeviceDashboardDto
                {
                    Id = device.Id,
                    Name = device.Name,
                    Type = device.Protocol.ToString()
                };

                var shares = await _shareService.ListShares(device.Id);
                allSharesCount += shares.Count();

                foreach (var share in shares)
                {
                    // Filter jobs for this share
                    var shareJobs = allJobs.Where(j => j.ShareId == share.Id).ToList();

                    // Find latest status
                    var latestJob = shareJobs.OrderByDescending(j => j.StartedAt).FirstOrDefault();
                    var lastSuccessfulJob = shareJobs.Where(j => j.Status == BackupJobStatus.Completed).OrderByDescending(j => j.StartedAt).FirstOrDefault();
                    
                    var isRunning = latestJob?.Status == BackupJobStatus.Running;
                    if (isRunning) runningJobs++;

                    // Calculate status
                    string status = "Pending";
                    if (!share.Enabled) 
                    {
                        status = "Disabled";
                    }
                    else if (isRunning)
                    {
                        status = "Running";
                    }
                    else if (latestJob != null)
                    {
                        if (latestJob.Status == BackupJobStatus.Completed) status = "Success";
                        else if (latestJob.Status == BackupJobStatus.Failed) status = "Failed";
                        else if (latestJob.Status == BackupJobStatus.PartiallyCompleted) status = "Warning";
                        else status = latestJob.Status.ToString();
                    }

                    // Recent failures (global stat)
                    var recentFailureCount = shareJobs.Count(j => j.Status == BackupJobStatus.Failed && j.CompletedAt >= yesterday);
                    recentFailures += recentFailureCount;

                    // Next Scheduled Run
                    DateTime? nextRun = null;
                    var cron = share.Schedule?.CronExpression ?? device.Schedule?.CronExpression;
                    if (!string.IsNullOrEmpty(cron))
                    {
                        nextRun = GetNextExecution(cron);
                    }

                    // Add to lists
                    deviceDto.Shares.Add(new ShareDashboardDto
                    {
                        Id = share.Id,
                        Name = share.Name,
                        Status = status,
                        LastBackupTime = lastSuccessfulJob?.CompletedAt,
                        LastBackupId = lastSuccessfulJob?.BackupId,
                        LastJobId = latestJob?.Id,
                        NextBackupTime = nextRun,
                        TotalSize = lastSuccessfulJob?.BytesTransferred ?? 0, // Using bytes transferred of last backup as proxy
                        FileCount = lastSuccessfulJob?.FilesProcessed ?? 0
                    });
                }
                
                // Determine Device Status
                // If any share running -> Running? Or just Online?
                // If any share failed recently -> Warning?
                // Let's keep it simple: "Online"
                // deviceDto.Status = "Online"; // Placeholder - Removed as property doesn't exist on DTO
                
                response.Devices.Add(deviceDto);
            }

            // Finalize Stats
            response.Stats.TotalDevices = devices.Count();
            response.Stats.TotalShares = allSharesCount;
            response.Stats.RecentFailures = recentFailures;
            response.Stats.RunningJobs = runningJobs;

            if (recentFailures > 0 && response.Stats.SystemHealth == "Healthy")
            {
                response.Stats.SystemHealth = "Warning";
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard summary");
            return StatusCode(500, new ErrorResponse { Error = "Failed to generate dashboard", Detail = ex.Message });
        }
    }

    private DateTime? GetNextExecution(string cron)
    {
        try
        {
            var expression = new CronExpression(cron);
            var next = expression.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
            return next?.UtcDateTime;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse cron expression: {Cron}", cron);
            return null;
        }
    }
}
