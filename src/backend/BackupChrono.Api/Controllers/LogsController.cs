using BackupChrono.Core.DTOs;
using BackupChrono.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BackupChrono.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogReaderService _logReaderService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogReaderService logReaderService, ILogger<LogsController> logger)
    {
        _logReaderService = logReaderService;
        _logger = logger;
    }

    /// <summary>
    /// Get application logs with optional filtering
    /// </summary>
    /// <param name="level">Filter by log level (Info, Warning, Error)</param>
    /// <param name="search">Search text in log messages</param>
    /// <param name="startDate">Start date filter (ISO 8601)</param>
    /// <param name="endDate">End date filter (ISO 8601)</param>
    /// <param name="limit">Maximum number of log entries to return (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of log entries</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LogEntry>>> GetLogs(
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] [Range(1, 1000)] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new LogQueryParameters
            {
                Level = level,
                Search = search,
                StartDate = startDate,
                EndDate = endDate,
                Limit = limit
            };

            var logs = await _logReaderService.GetLogsAsync(parameters, cancellationToken);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs");
            return StatusCode(500, new { error = "Failed to retrieve logs" });
        }
    }
}
