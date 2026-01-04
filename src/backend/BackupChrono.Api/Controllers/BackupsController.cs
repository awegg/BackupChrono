using BackupChrono.Api.DTOs;
using BackupChrono.Core.DTOs;
using BackupChrono.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BackupChrono.Api.Controllers;

/// <summary>
/// Controller for backup browsing and restore operations
/// </summary>
[ApiController]
[Route("api/backups")]
public class BackupsController : ControllerBase
{
    private readonly IResticService _resticService;
    private readonly ILogger<BackupsController> _logger;

    public BackupsController(
        IResticService resticService,
        ILogger<BackupsController> logger)
    {
        _resticService = resticService;
        _logger = logger;
    }

    /// <summary>
    /// List all backups with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BackupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BackupDto>>> ListBackups(
        [FromQuery] Guid? deviceId = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            // TODO: Filter by deviceId when provided
            var backups = await _resticService.ListBackups();

            var backupDtos = backups.Select(b => new BackupDto
            {
                Id = b.Id,
                DeviceId = b.DeviceId,
                ShareId = b.ShareId,
                DeviceName = b.DeviceName,
                ShareName = b.ShareName,
                Timestamp = b.Timestamp,
                Status = b.Status.ToString(),
                SharesPaths = b.SharesPaths,
                FilesNew = b.FilesNew,
                FilesChanged = b.FilesChanged,
                FilesUnmodified = b.FilesUnmodified,
                DataAdded = b.DataAdded,
                DataProcessed = b.DataProcessed,
                Duration = b.Duration.ToString(),
                ErrorMessage = b.ErrorMessage,
                CreatedByJobId = b.CreatedByJobId
            }).Take(limit).ToList();

            return Ok(backupDtos);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("repository does not exist"))
        {
            // Repository doesn't exist yet - return empty list
            _logger.LogInformation("Repository does not exist yet, returning empty backup list");
            return Ok(new List<BackupDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing backups");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to list backups",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get backup details by ID
    /// </summary>
    [HttpGet("{backupId}")]
    [ProducesResponseType(typeof(BackupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupDto>> GetBackup(string backupId)
    {
        try
        {
            var backup = await _resticService.GetBackup(backupId);

            var backupDto = new BackupDto
            {
                Id = backup.Id,
                DeviceId = backup.DeviceId,
                ShareId = backup.ShareId,
                DeviceName = backup.DeviceName,
                ShareName = backup.ShareName,
                Timestamp = backup.Timestamp,
                Status = backup.Status.ToString(),
                SharesPaths = backup.SharesPaths,
                FilesNew = backup.FilesNew,
                FilesChanged = backup.FilesChanged,
                FilesUnmodified = backup.FilesUnmodified,
                DataAdded = backup.DataAdded,
                DataProcessed = backup.DataProcessed,
                Duration = backup.Duration.ToString(),
                ErrorMessage = backup.ErrorMessage,
                CreatedByJobId = backup.CreatedByJobId
            };

            return Ok(backupDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = $"No backup with ID {backupId}"
            });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("repository does not exist") || 
            ex.Message.Contains("exit code 10") ||
            ex.Message.Contains("exit code 1") ||
            ex.Message.Contains("unable to open config file"))
        {
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = $"No backup with ID {backupId} (repository does not exist)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup {BackupId}", backupId);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to get backup",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Browse files in a backup snapshot
    /// </summary>
    [HttpGet("{backupId}/files")]
    [ProducesResponseType(typeof(List<FileEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<FileEntry>>> BrowseBackupFiles(
        string backupId,
        [FromQuery] Guid deviceId,
        [FromQuery] Guid shareId,
        [FromQuery] string path = "/")
    {
        try
        {
            // Construct repository path
            var repositoryPath = Path.Combine("./repositories", deviceId.ToString(), shareId.ToString());
            
            var files = await _resticService.BrowseBackup(backupId, path, repositoryPath);
            var filesList = files.ToList();
            
            _logger.LogInformation(
                "Browsing backup {BackupId} at path {Path}: found {FileCount} items",
                backupId,
                path,
                filesList.Count);
            
            return Ok(filesList);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = $"No backup with ID {backupId}"
            });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("repository does not exist") || 
            ex.Message.Contains("exit code 10") ||
            ex.Message.Contains("exit code 1") ||
            ex.Message.Contains("unable to open config file"))
        {
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = $"No backup with ID {backupId} (repository does not exist)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing backup {BackupId} at path {Path}", backupId, path);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to browse backup",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Download a single file from a backup
    /// </summary>
    [HttpGet("{backupId}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(
        string backupId,
        [FromQuery] Guid deviceId,
        [FromQuery] Guid shareId,
        [FromQuery] string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Invalid request",
                    Detail = "filePath is required"
                });
            }

            // Construct repository path
            var repositoryPath = Path.Combine("./repositories", deviceId.ToString(), shareId.ToString());

            _logger.LogInformation(
                "Download requested for file {FilePath} from backup {BackupId}",
                filePath,
                backupId);

            // Use restic dump to stream file contents directly without loading into memory
            var fileStream = await _resticService.DumpFileStream(backupId, filePath, repositoryPath);
            var fileName = Path.GetFileName(filePath);

            return File(fileStream, "application/octet-stream", fileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Backup {BackupId} not found: {Message}", backupId, ex.Message);
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FilePath} from backup {BackupId}", filePath, backupId);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to download file",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Restore files from a backup
    /// </summary>
    [HttpPost("{backupId}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreBackup(
        string backupId,
        [FromQuery] Guid deviceId,
        [FromQuery] Guid shareId,
        [FromBody] RestoreRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TargetPath))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Invalid restore request",
                    Detail = "TargetPath is required"
                });
            }

            // Determine target path - if RestoreToSource is true, we would need to get the original path
            // For now, we use the provided TargetPath in all cases
            var targetPath = request.TargetPath;
            
            // Convert IncludePaths from List to array for service call
            var includePaths = request.IncludePaths?.ToArray();

            // Construct repository path
            var repositoryPath = Path.Combine("./repositories", deviceId.ToString(), shareId.ToString());
            
            _logger.LogInformation(
                "Restore requested for backup {BackupId} to {TargetPath} with {PathCount} include paths",
                backupId,
                targetPath,
                includePaths?.Length ?? 0);

            // Execute the restore operation
            await _resticService.RestoreBackup(backupId, targetPath, includePaths, repositoryPath);
            
            // Generate a restore ID for tracking (for future progress tracking implementation)
            var restoreId = Guid.NewGuid().ToString();

            return Ok(new
            {
                restoreId,
                status = "Completed",
                targetPath
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Backup {BackupId} not found: {Message}", backupId, ex.Message);
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup {BackupId}", backupId);
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to restore backup",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get version history for a specific file across all backups
    /// </summary>
    [HttpGet("files/history")]
    [ProducesResponseType(typeof(List<FileVersion>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<ActionResult<List<FileVersion>>> GetFileHistory(
        [FromQuery] Guid deviceId,
        [FromQuery] string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult<ActionResult<List<FileVersion>>>(BadRequest(new ErrorResponse
                {
                    Error = "Invalid request",
                    Detail = "filePath is required"
                }));
            }

            // TODO: Implement file history tracking
            // For now return empty list
            return Task.FromResult<ActionResult<List<FileVersion>>>(Ok(new List<FileVersion>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file history for {FilePath}", filePath);
            return Task.FromResult<ActionResult<List<FileVersion>>>(StatusCode(500, new ErrorResponse
            {
                Error = "Failed to get file history",
                Detail = ex.Message
            }));
        }
    }
}
