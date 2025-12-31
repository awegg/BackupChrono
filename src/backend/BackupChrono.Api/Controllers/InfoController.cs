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

    /// <summary>
    /// Initializes a new instance of the InfoController with its required dependencies.
    /// </summary>
    /// <param name="logger">Logger used by the controller for diagnostic and audit messages.</param>
    public InfoController(ILogger<InfoController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get API version and information
    /// <summary>
    /// Provides basic API metadata and the current UTC timestamp.
    /// </summary>
    /// <returns>An object with the fields `Name`, `Version`, `Description`, and `Timestamp` (UTC).</returns>
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