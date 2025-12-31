using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

/// <summary>
/// API information and status controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InfoController : ControllerBase
{
    private readonly ILogger<InfoController> _logger;

    public InfoController(ILogger<InfoController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get API version and information
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            Name = "BackupChrono API",
            Version = "1.0.0",
            Description = "Version-controlled backup orchestration system",
            Timestamp = DateTime.UtcNow
        });
    }
}
