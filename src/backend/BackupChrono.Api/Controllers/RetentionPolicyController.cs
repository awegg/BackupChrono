using BackupChrono.Core.DTOs;
using BackupChrono.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

/// <summary>
/// Controller for retention policy operations.
/// </summary>
[ApiController]
[Route("api/retention-policy")]
public class RetentionPolicyController : ControllerBase
{
    private readonly IRetentionPolicyService _retentionPolicyService;
    private readonly ILogger<RetentionPolicyController> _logger;

    public RetentionPolicyController(
        IRetentionPolicyService retentionPolicyService,
        ILogger<RetentionPolicyController> logger)
    {
        _retentionPolicyService = retentionPolicyService;
        _logger = logger;
    }

    /// <summary>
    /// Manually execute retention policy for all devices.
    /// </summary>
    /// <param name="dryRun">If true, only simulate without actually pruning snapshots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each device/share processed.</returns>
    [HttpPost("execute")]
    public async Task<ActionResult<IEnumerable<RetentionRunResult>>> ExecuteRetentionPolicy(
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual retention policy execution requested (dry-run={DryRun})", dryRun);

        try
        {
            var results = await _retentionPolicyService.ApplyForAll(dryRun, cancellationToken);
            return Ok(results);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Retention policy execution was cancelled");
            return StatusCode(499, "Retention policy execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute retention policy");
            return StatusCode(500, new { error = "Failed to execute retention policy", details = ex.Message });
        }
    }

    /// <summary>
    /// Manually execute retention policy for a specific device.
    /// </summary>
    /// <param name="deviceName">Device name to apply retention for.</param>
    /// <param name="dryRun">If true, only simulate without actually pruning snapshots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for the device and its shares.</returns>
    [HttpPost("execute/{deviceName}")]
    public async Task<ActionResult<IEnumerable<RetentionRunResult>>> ExecuteRetentionPolicyForDevice(
        string deviceName,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual retention policy execution for device {DeviceName} (dry-run={DryRun})", deviceName, dryRun);

        try
        {
            var results = await _retentionPolicyService.ApplyForDevice(deviceName, dryRun, cancellationToken);
            
            if (!results.Any())
            {
                return NotFound(new { error = $"Device '{deviceName}' not found or has no valid retention policy" });
            }

            return Ok(results);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Retention policy execution for device {DeviceName} was cancelled", deviceName);
            return StatusCode(499, "Retention policy execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute retention policy for device {DeviceName}", deviceName);
            return StatusCode(500, new { error = $"Failed to execute retention policy for device '{deviceName}'", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the last retention policy run summary.
    /// </summary>
    /// <returns>Summary of last run or null if no runs have been executed.</returns>
    [HttpGet("last-run")]
    public async Task<ActionResult<RetentionLastRun?>> GetLastRun()
    {
        var lastRun = await _retentionPolicyService.GetLastRun();
        return Ok(lastRun);
    }
}
