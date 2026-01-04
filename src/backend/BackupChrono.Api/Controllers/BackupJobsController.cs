using BackupChrono.Api.DTOs;
using BackupChrono.Api.Services;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

[ApiController]
[Route("api/backup-jobs")]
public class BackupJobsController : ControllerBase
{
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IQuartzSchedulerService _schedulerService;
    private readonly IMappingService _mappingService;
    private readonly ILogger<BackupJobsController> _logger;

    public BackupJobsController(
        IBackupJobRepository backupJobRepository,
        IQuartzSchedulerService schedulerService,
        IMappingService mappingService,
        ILogger<BackupJobsController> logger)
    {
        _backupJobRepository = backupJobRepository;
        _schedulerService = schedulerService;
        _mappingService = mappingService;
        _logger = logger;
    }

    /// <summary>
    /// List backup jobs
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BackupJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BackupJobDto>>> ListBackupJobs(
        [FromQuery] Guid? deviceId = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            // TODO: Implement filtering in repository
            var jobs = await _backupJobRepository.ListJobs();
            
            // Apply filters
            if (deviceId.HasValue)
            {
                jobs = jobs.Where(j => j.DeviceId == deviceId.Value).ToList();
            }
            
            if (!string.IsNullOrEmpty(status))
            {
                jobs = jobs.Where(j => j.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            // Apply limit
            jobs = jobs.Take(limit).ToList();
            
            var dtos = jobs.Select(_mappingService.ToBackupJobDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing backup jobs");
            return StatusCode(500, new ErrorResponse { Error = "Failed to list backup jobs", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Get backup job by ID
    /// </summary>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(BackupJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupJobDto>> GetBackupJob(Guid jobId)
    {
        try
        {
            var job = await _backupJobRepository.GetJob(jobId);
            if (job == null)
            {
                return NotFound(new ErrorResponse { Error = "Backup job not found", Detail = $"No job with ID {jobId}" });
            }

            var dto = _mappingService.ToBackupJobDto(job);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup job {JobId}", jobId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to get backup job", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Trigger a manual backup
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerBackup([FromBody] TriggerBackupRequest? request)
    {
        try
        {
            // Validate that request exists and DeviceId is provided
            if (request == null || request.DeviceId == Guid.Empty)
            {
                return BadRequest(new ErrorResponse { Error = "Invalid backup request", Detail = "DeviceId is required" });
            }

            _logger.LogInformation(
                "Triggering manual backup for device {DeviceId}, share {ShareId}",
                request.DeviceId,
                request.ShareId);

            await _schedulerService.TriggerImmediateBackup(request.DeviceId, request.ShareId);
            
            return Accepted(new { message = "Backup triggered successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid backup trigger request");
            return BadRequest(new ErrorResponse { Error = "Invalid backup request", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering backup");
            return StatusCode(500, new ErrorResponse { Error = "Failed to trigger backup", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a running backup job
    /// </summary>
    [HttpPost("{jobId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBackupJob(Guid jobId)
    {
        try
        {
            var job = await _backupJobRepository.GetJob(jobId);
            if (job == null)
            {
                return NotFound(new ErrorResponse { Error = "Backup job not found", Detail = $"No job with ID {jobId}" });
            }

            await _schedulerService.CancelJob(jobId);
            _logger.LogInformation("Cancelled backup job {JobId}", jobId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling backup job {JobId}", jobId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to cancel backup job", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Delete a backup job by ID
    /// </summary>
    [HttpDelete("{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBackupJob(Guid jobId)
    {
        try
        {
            var deleted = await _backupJobRepository.DeleteJob(jobId);
            if (!deleted)
            {
                return NotFound(new ErrorResponse { Error = "Backup job not found", Detail = $"No job with ID {jobId}" });
            }

            _logger.LogInformation("Deleted backup job {JobId}", jobId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup job {JobId}", jobId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to delete backup job", Detail = ex.Message });
        }
    }
}
