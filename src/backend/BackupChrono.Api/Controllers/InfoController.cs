using Microsoft.AspNetCore.Mvc;
using System.Reflection;

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
    /// Provides basic API metadata and the current UTC timestamp.
    /// </summary>
    /// <returns>An object with the fields `Name`, `Version`, `Description`, and `Timestamp` (UTC).</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "0.0.0";

        return Ok(new
        {
            Name = "BackupChrono API",
            Version = version,
            Description = "Version-controlled backup orchestration system",
            Timestamp = DateTime.UtcNow
        });
    }
}