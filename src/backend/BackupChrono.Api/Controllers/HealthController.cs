using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using BackupChrono.Infrastructure.Plugins;
using BackupChrono.Infrastructure.Restic;
using Microsoft.AspNetCore.Mvc;

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

    public HealthController(ILogger<HealthController> logger, ResticClient resticClient)
    {
        _logger = logger;
        _resticClient = resticClient;
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
            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
        };

        try
        {
            // Check restic binary availability
            var resticVersionOutput = await _resticClient.ExecuteCommand(new[] { "version" });
            healthStatus.ResticAvailable = !string.IsNullOrWhiteSpace(resticVersionOutput);
            healthStatus.ResticVersion = resticVersionOutput.Split('\n').FirstOrDefault()?.Trim() ?? "unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restic health check failed");
            healthStatus.ResticAvailable = false;
            healthStatus.Status = "Degraded";
        }

        return Ok(healthStatus);
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
    public bool ResticAvailable { get; set; }
    public string? ResticVersion { get; set; }
}
