using BackupChrono.Api.DTOs;
using BackupChrono.Api.Services;
using BackupChrono.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:guid}/shares")]
public class SharesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;
    private readonly IMappingService _mappingService;
    private readonly IQuartzSchedulerService _schedulerService;
    private readonly ILogger<SharesController> _logger;

    public SharesController(
        IDeviceService deviceService,
        IShareService shareService,
        IMappingService mappingService,
        IQuartzSchedulerService schedulerService,
        ILogger<SharesController> logger)
    {
        _deviceService = deviceService;
        _shareService = shareService;
        _mappingService = mappingService;
        _schedulerService = schedulerService;
        _logger = logger;
    }

    /// <summary>
    /// List shares for a device
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ShareDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ShareDto>>> ListShares(Guid deviceId)
    {
        try
        {
            // Verify device exists
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            var shares = await _shareService.ListShares(deviceId);
            var dtos = shares.Select(_mappingService.ToShareDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing shares for device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to list shares", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Get share by ID
    /// </summary>
    [HttpGet("{shareId:guid}")]
    [ProducesResponseType(typeof(ShareDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareDetailDto>> GetShare(Guid deviceId, Guid shareId)
    {
        try
        {
            var share = await _shareService.GetShare(shareId);
            if (share == null || share.DeviceId != deviceId)
            {
                return NotFound(new ErrorResponse { Error = "Share not found", Detail = $"No share with ID {shareId} for device {deviceId}" });
            }

            // TODO: Get last backup from backup repository
            var dto = _mappingService.ToShareDetailDto(share);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting share {ShareId} for device {DeviceId}", shareId, deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to get share", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Create a new share
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShareDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareDto>> CreateShare(Guid deviceId, [FromBody] ShareCreateDto dto)
    {
        try
        {
            // Verify device exists
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            var share = _mappingService.ToShare(deviceId, dto);
            var created = await _shareService.CreateShare(share);
            var responseDto = _mappingService.ToShareDto(created);
            
            // Schedule backups if share has a schedule
            if (created.Schedule != null)
            {
                await _schedulerService.ScheduleShareBackup(device, created, created.Schedule);
            }
            
            return CreatedAtAction(nameof(GetShare), new { deviceId, shareId = created.Id }, responseDto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid share creation request for device {DeviceId}", deviceId);
            return BadRequest(new ErrorResponse { Error = "Invalid share data", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating share for device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to create share", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Update share
    /// </summary>
    [HttpPut("{shareId:guid}")]
    [ProducesResponseType(typeof(ShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareDto>> UpdateShare(Guid deviceId, Guid shareId, [FromBody] ShareUpdateDto dto)
    {
        try
        {
            var share = await _shareService.GetShare(shareId);
            if (share == null || share.DeviceId != deviceId)
            {
                return NotFound(new ErrorResponse { Error = "Share not found", Detail = $"No share with ID {shareId} for device {deviceId}" });
            }

            var oldSchedule = share.Schedule;
            _mappingService.ApplyUpdate(share, dto);
            var updated = await _shareService.UpdateShare(share);
            var responseDto = _mappingService.ToShareDto(updated);
            
            // Update scheduler if schedule changed
            var scheduleChanged = (oldSchedule == null && updated.Schedule != null) ||
                                  (oldSchedule != null && updated.Schedule == null) ||
                                  (oldSchedule != null && updated.Schedule != null && oldSchedule.CronExpression != updated.Schedule.CronExpression);
            
            if (scheduleChanged)
            {
                // Unschedule old job
                await _schedulerService.UnscheduleShareBackup(shareId);
                
                // Schedule new job if schedule exists
                if (updated.Schedule != null)
                {
                    var device = await _deviceService.GetDevice(deviceId);
                    if (device != null)
                    {
                        await _schedulerService.ScheduleShareBackup(device, updated, updated.Schedule);
                    }
                }
            }
            
            return Ok(responseDto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid share update request for share {ShareId}", shareId);
            return BadRequest(new ErrorResponse { Error = "Invalid share data", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating share {ShareId} for device {DeviceId}", shareId, deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to update share", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Delete share
    /// </summary>
    [HttpDelete("{shareId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteShare(Guid deviceId, Guid shareId)
    {
        try
        {
            var share = await _shareService.GetShare(shareId);
            if (share == null || share.DeviceId != deviceId)
            {
                return NotFound(new ErrorResponse { Error = "Share not found", Detail = $"No share with ID {shareId} for device {deviceId}" });
            }

            // Unschedule share backups
            await _schedulerService.UnscheduleShareBackup(shareId);
            
            await _shareService.DeleteShare(shareId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting share {ShareId} for device {DeviceId}", shareId, deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to delete share", Detail = ex.Message });
        }
    }
}
