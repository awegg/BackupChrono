using BackupChrono.Api.DTOs;
using BackupChrono.Api.Services;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;
    private readonly IMappingService _mappingService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceService deviceService,
        IShareService shareService,
        IMappingService mappingService,
        ILogger<DevicesController> logger)
    {
        _deviceService = deviceService;
        _shareService = shareService;
        _mappingService = mappingService;
        _logger = logger;
    }

    /// <summary>
    /// List all devices
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeviceDto>>> ListDevices()
    {
        try
        {
            var devices = await _deviceService.ListDevices();
            var dtos = devices.Select(_mappingService.ToDeviceDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing devices");
            return StatusCode(500, new ErrorResponse { Error = "Failed to list devices", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    [HttpGet("{deviceId:guid}")]
    [ProducesResponseType(typeof(DeviceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDetailDto>> GetDevice(Guid deviceId)
    {
        try
        {
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            var shares = await _shareService.ListShares(deviceId);
            
            // TODO: Get last backup from backup repository
            var dto = _mappingService.ToDeviceDetailDto(device, shares.ToList());
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to get device", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Create a new device
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeviceDto>> CreateDevice([FromBody] DeviceCreateDto dto)
    {
        try
        {
            var device = _mappingService.ToDevice(dto);
            var created = await _deviceService.CreateDevice(device);
            var responseDto = _mappingService.ToDeviceDto(created);
            
            return CreatedAtAction(nameof(GetDevice), new { deviceId = created.Id }, responseDto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid device creation request");
            return BadRequest(new ErrorResponse { Error = "Invalid device data", Detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid device creation request");
            return BadRequest(new ErrorResponse { Error = "Invalid device data", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating device");
            return StatusCode(500, new ErrorResponse { Error = "Failed to create device", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Update device
    /// </summary>
    [HttpPut("{deviceId:guid}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> UpdateDevice(Guid deviceId, [FromBody] DeviceUpdateDto dto)
    {
        try
        {
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            _mappingService.ApplyUpdate(device, dto);
            var updated = await _deviceService.UpdateDevice(device);
            var responseDto = _mappingService.ToDeviceDto(updated);
            
            return Ok(responseDto);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(ex, "Invalid device update request for device {DeviceId}", deviceId);
            return BadRequest(new ErrorResponse { Error = "Invalid device data", Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to update device", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Delete device
    /// </summary>
    [HttpDelete("{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDevice(Guid deviceId)
    {
        try
        {
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            await _deviceService.DeleteDevice(deviceId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to delete device", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Test device connection
    /// </summary>
    [HttpPost("{deviceId:guid}/test-connection")]
    [ProducesResponseType(typeof(ConnectionTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection(Guid deviceId)
    {
        try
        {
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            var startTime = DateTime.UtcNow;
            var success = await _deviceService.TestConnection(deviceId);
            var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return Ok(new ConnectionTestResult
            {
                Success = success,
                Message = success ? "Connection successful" : "Connection failed",
                Latency = success ? latency : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection for device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to test connection", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Send Wake-on-LAN packet to device
    /// </summary>
    [HttpPost("{deviceId:guid}/wake")]
    [ProducesResponseType(typeof(WakeOnLanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WakeOnLanResult>> WakeDevice(Guid deviceId)
    {
        try
        {
            var device = await _deviceService.GetDevice(deviceId);
            if (device == null)
            {
                return NotFound(new ErrorResponse { Error = "Device not found", Detail = $"No device with ID {deviceId}" });
            }

            if (!device.WakeOnLanEnabled || string.IsNullOrEmpty(device.WakeOnLanMacAddress))
            {
                return BadRequest(new ErrorResponse 
                { 
                    Error = "Wake-on-LAN not enabled", 
                    Detail = "WOL is not enabled for this device or MAC address is not configured" 
                });
            }

            await WakeOnLanHelper.SendMagicPacket(device.WakeOnLanMacAddress);
            
            _logger.LogInformation(
                "Wake-on-LAN magic packet sent to device {DeviceName} ({DeviceId}) at MAC address {MacAddress}",
                device.Name,
                deviceId,
                device.WakeOnLanMacAddress
            );
            
            return Ok(new WakeOnLanResult
            {
                Success = true,
                Message = $"Wake-on-LAN magic packet sent to {device.WakeOnLanMacAddress}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WOL to device {DeviceId}", deviceId);
            return StatusCode(500, new ErrorResponse { Error = "Failed to send WOL packet", Detail = ex.Message });
        }
    }
}
