using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using BackupChrono.Infrastructure.Plugins;
using BackupChrono.Infrastructure.Restic;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BackupChrono.Api.Controllers;

/// <summary>
/// Health check endpoint for monitoring application status.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly ResticClient _resticClient;
    private readonly GitConfigService _gitConfigService;
    private readonly IStorageMonitor _storageMonitor;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public HealthController(
        ILogger<HealthController> logger, 
        ResticClient resticClient,
        GitConfigService gitConfigService,
        IStorageMonitor storageMonitor)
    {
        _logger = logger;
        _resticClient = resticClient;
        _gitConfigService = gitConfigService;
        _storageMonitor = storageMonitor;
    }

    /// <summary>
    /// Gets the health status of the application.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var healthStatus = new HealthStatus
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            Uptime = DateTime.UtcNow - _startTime
        };

        var checks = new List<HealthCheck>();

        // Check restic binary availability
        checks.Add(await CheckRestic());

        // Check Git repository
        checks.Add(CheckGitRepository());

        // Check storage capacity
        checks.Add(await CheckStorageCapacity());

        // Check system resources
        checks.Add(CheckSystemResources());

        healthStatus.Checks = checks;

        // Determine overall status
        if (checks.Any(c => c.Status == "Critical"))
        {
            healthStatus.Status = "Unhealthy";
        }
        else if (checks.Any(c => c.Status == "Warning"))
        {
            healthStatus.Status = "Degraded";
        }

        return Ok(healthStatus);
    }

    /// <summary>
    /// Gets detailed health information including all subsystems.
    /// </summary>
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailed()
    {
        var result = await Get();
        return result;
    }

    private async Task<HealthCheck> CheckRestic()
    {
        try
        {
            var resticVersionOutput = await _resticClient.ExecuteCommand(new[] { "version" });
            var versionLine = resticVersionOutput.Split('\n').FirstOrDefault()?.Trim() ?? "unknown";
            
            return new HealthCheck
            {
                Name = "Restic",
                Status = "Healthy",
                Message = $"Available: {versionLine}",
                Details = new Dictionary<string, object>
                {
                    ["version"] = versionLine,
                    ["available"] = true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restic health check failed");
            return new HealthCheck
            {
                Name = "Restic",
                Status = "Critical",
                Message = "Restic binary not available or not executable",
                Details = new Dictionary<string, object>
                {
                    ["available"] = false,
                    ["error"] = ex.Message
                }
            };
        }
    }

    private HealthCheck CheckGitRepository()
    {
        try
        {
            var repoPath = _gitConfigService.RepositoryPath;
            var exists = Directory.Exists(Path.Combine(repoPath, ".git"));
            
            return new HealthCheck
            {
                Name = "Git Repository",
                Status = exists ? "Healthy" : "Warning",
                Message = exists ? "Git repository accessible" : "Git repository not initialized",
                Details = new Dictionary<string, object>
                {
                    ["path"] = repoPath,
                    ["initialized"] = exists
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Name = "Git Repository",
                Status = "Warning",
                Message = "Error accessing Git repository",
                Details = new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }

    private async Task<HealthCheck> CheckStorageCapacity()
    {
        try
        {
            var storageStatuses = await _storageMonitor.GetAllRepositoryStorageStatus();
            
            if (!storageStatuses.Any())
            {
                return new HealthCheck
                {
                    Name = "Storage Capacity",
                    Status = "Healthy",
                    Message = "No repositories configured yet",
                    Details = new Dictionary<string, object>
                    {
                        ["repositories"] = 0
                    }
                };
            }

            var worstStatus = storageStatuses
                .OrderByDescending(s => s.ThresholdLevel)
                .First();

            var statusMap = new Dictionary<StorageThresholdLevel, string>
            {
                [StorageThresholdLevel.Normal] = "Healthy",
                [StorageThresholdLevel.Warning] = "Warning",
                [StorageThresholdLevel.Critical] = "Critical",
                [StorageThresholdLevel.Exhausted] = "Critical"
            };

            return new HealthCheck
            {
                Name = "Storage Capacity",
                Status = statusMap[worstStatus.ThresholdLevel],
                Message = worstStatus.Message,
                Details = new Dictionary<string, object>
                {
                    ["path"] = worstStatus.Path,
                    ["usedPercentage"] = worstStatus.UsedPercentage,
                    ["availableGB"] = worstStatus.AvailableBytes / 1024.0 / 1024.0 / 1024.0,
                    ["totalGB"] = worstStatus.TotalBytes / 1024.0 / 1024.0 / 1024.0,
                    ["thresholdLevel"] = worstStatus.ThresholdLevel.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Name = "Storage Capacity",
                Status = "Warning",
                Message = "Unable to check storage capacity",
                Details = new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }

    private HealthCheck CheckSystemResources()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / 1024 / 1024;
            var cpuTime = process.TotalProcessorTime;
            
            // Warning if memory usage exceeds 500 MB
            var status = memoryMB > 500 ? "Warning" : "Healthy";
            
            return new HealthCheck
            {
                Name = "System Resources",
                Status = status,
                Message = $"Memory: {memoryMB} MB, CPU Time: {cpuTime.TotalMinutes:F1} min",
                Details = new Dictionary<string, object>
                {
                    ["memoryMB"] = memoryMB,
                    ["cpuTimeSeconds"] = cpuTime.TotalSeconds,
                    ["threadCount"] = process.Threads.Count
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Name = "System Resources",
                Status = "Warning",
                Message = "Unable to retrieve system resource information",
                Details = new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }
            };
        }
    }
}

/// <summary>
/// Health status response model.
/// </summary>
public class HealthStatus
{
    public required string Status { get; set; }
    public DateTime Timestamp { get; set; }
    public required string Version { get; set; }
    public TimeSpan Uptime { get; set; }
    public IEnumerable<HealthCheck> Checks { get; set; } = Array.Empty<HealthCheck>();
}

/// <summary>
/// Individual health check result.
/// </summary>
public class HealthCheck
{
    public required string Name { get; set; }
    public required string Status { get; set; } // Healthy, Warning, Critical
    public required string Message { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}
