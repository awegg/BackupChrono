using BackupChrono.Api.DTOs;
using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BackupChrono.Api.Controllers;

/// <summary>
/// Controller for backup browsing and restore operations
/// </summary>
[ApiController]
[Route("api/backups")]
public class BackupsController : ControllerBase
{
    private readonly IResticService _resticService;
    private readonly IBackupLogService _backupLogService;
    private readonly ResticOptions _resticOptions;
    private readonly ILogger<BackupsController> _logger;

    public BackupsController(
        IResticService resticService,
            IBackupLogService backupLogService,
        IOptions<ResticOptions> resticOptions,
        ILogger<BackupsController> logger)
    {
        _resticService = resticService;
        _backupLogService = backupLogService;
        _resticOptions = resticOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Maps a Backup entity to BackupDto.
    /// </summary>
    private static BackupDto MapToBackupDto(Backup backup)
    {
        return new BackupDto
        {
            Id = backup.Id,
            DeviceId = backup.DeviceId,
            ShareId = backup.ShareId,
            DeviceName = backup.DeviceName,
            ShareName = backup.ShareName,
            Timestamp = backup.Timestamp,
            Status = backup.Status.ToString(),
            SharesPaths = backup.SharesPaths,
            FileStats = new FileStatsDto
            {
                New = backup.FilesNew,
                Changed = backup.FilesChanged,
                Unmodified = backup.FilesUnmodified
            },
            DataStats = new DataStatsDto
            {
                Added = backup.DataAdded,
                Processed = backup.DataProcessed
            },
            Duration = System.Xml.XmlConvert.ToString(backup.Duration), // ISO 8601 format
            ErrorMessage = backup.ErrorMessage,
            CreatedByJobId = backup.CreatedByJobId
        };
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

            var backupDtos = backups.Select(MapToBackupDto).Take(limit).ToList();

            return Ok(backupDtos);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("repository does not exist") ||
            ex.Message.Contains("exit code 1") ||
            ex.Message.Contains("unable to open config file"))
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
    [ProducesResponseType(typeof(BackupDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupDetailDto>> GetBackup(
        string backupId,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] Guid? shareId = null)
    {
        // Declare repositoryPath outside try block so it can be accessed in catch blocks
        string? repositoryPath = null;
        
        try
        {
            _logger.LogInformation("GetBackup called: backupId={BackupId}, deviceId={DeviceId}, shareId={ShareId}", backupId, deviceId, shareId);
            
            // Construct repository path if device/share provided
            if (deviceId.HasValue && shareId.HasValue)
            {
                repositoryPath = Path.Combine(_resticOptions.RepositoryBasePath, deviceId.Value.ToString(), shareId.Value.ToString());
                _logger.LogInformation("Repository path constructed: {RepositoryPath}", repositoryPath);
            }
            else
            {
                _logger.LogWarning("No deviceId/shareId provided, repository path will be null");
            }

            _logger.LogInformation("Calling ResticService.GetBackupDetailComplete with repositoryPath={RepositoryPath}", repositoryPath);
            var (backup, metadata, stats) = await _resticService.GetBackupDetailComplete(backupId, repositoryPath);
            
            var backupDetail = new BackupDetailDto
            {
                Id = backup.Id,
                DeviceId = backup.DeviceId,
                ShareId = backup.ShareId,
                DeviceName = backup.DeviceName,
                ShareName = backup.ShareName,
                Timestamp = backup.Timestamp,
                Status = backup.Status.ToString(),
                SharesPaths = backup.SharesPaths,
                FileStats = new FileStatsDto
                {
                    New = metadata?.Summary?.FilesNew ?? backup.FilesNew,
                    Changed = metadata?.Summary?.FilesChanged ?? backup.FilesChanged,
                    Unmodified = metadata?.Summary?.FilesUnmodified ?? backup.FilesUnmodified
                },
                DataStats = new DataStatsDto
                {
                    Added = metadata?.Summary?.DataAdded ?? backup.DataAdded,
                    Processed = metadata?.Summary?.DataProcessed ?? backup.DataProcessed
                },
                Duration = System.Xml.XmlConvert.ToString(backup.Duration),
                ErrorMessage = backup.ErrorMessage,
                CreatedByJobId = backup.CreatedByJobId,
                
                // Extended fields from metadata
                DirectoryStats = new DirectoryStatsDto
                {
                    New = metadata?.Summary?.DirsNew ?? 0,
                    Changed = metadata?.Summary?.DirsChanged ?? 0,
                    Unmodified = metadata?.Summary?.DirsUnmodified ?? 0
                },
                SnapshotInfo = new SnapshotInfoDto
                {
                    SnapshotId = backup.Id,
                    ParentSnapshot = metadata?.Parent,
                    ExitCode = backup.Status == BackupStatus.Success ? 0 : 1
                },
                DeduplicationInfo = new DeduplicationInfoDto
                {
                    DataBlobs = (int)(stats?.TotalBlobCount ?? 0),
                    TreeBlobs = (int)(stats?.TotalTreeCount ?? 0),
                    Ratio = stats != null ? $"{stats.DeduplicationRatio:P1}" : "0%",
                    SpaceSaved = stats != null ? FormatBytes(stats.DeduplicationSpaceSaved) : "0 B",
                    ContentDedup = CalculateContentDedup(metadata?.Summary?.DataAdded ?? backup.DataAdded, metadata?.Summary?.DataProcessed ?? backup.DataProcessed),
                    UniqueStorage = stats != null ? FormatBytes(stats.TotalSize) : "0 B"
                },
                Shares = metadata?.Paths.Select(path => new BackupShareDto
                {
                    Name = System.IO.Path.GetFileName(path) ?? path,
                    Path = path
                }).ToList() ?? new List<BackupShareDto>()
            };

            return Ok(backupDetail);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Backup not found: {BackupId} with repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "null");
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
            _logger.LogError(ex, "Repository error for backup {BackupId} with repositoryPath={RepositoryPath}, message: {Message}", backupId, repositoryPath ?? "null", ex.Message);
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = $"No backup with ID {backupId} (repository does not exist)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup {BackupId} with repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "null");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to get backup",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Formats bytes to human-readable string.
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Get backup execution logs
    /// </summary>
    [HttpGet("{backupId}/logs")]
    [ProducesResponseType(typeof(BackupLogsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackupLogsDto>> GetBackupLogs(
        string backupId,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] Guid? shareId = null)
    {
        // Declare repositoryPath outside try block so it can be accessed in catch blocks
        string? repositoryPath = null;
        
        try
        {
            _logger.LogInformation("GetBackupLogs called: backupId={BackupId}, deviceId={DeviceId}, shareId={ShareId}", backupId, deviceId, shareId);
            
            // Construct repository path if device/share provided
            if (deviceId.HasValue && shareId.HasValue)
            {
                repositoryPath = Path.Combine(_resticOptions.RepositoryBasePath, deviceId.Value.ToString(), shareId.Value.ToString());
                _logger.LogInformation("Repository path constructed: {RepositoryPath}", repositoryPath);
            }
            else
            {
                _logger.LogWarning("No deviceId/shareId provided for logs, repository path will be null");
            }

            // Verify backup exists
            _logger.LogInformation("Verifying backup exists: {BackupId} with repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "null");
            var backup = await _resticService.GetBackup(backupId, repositoryPath);
            
                // Get logs from backup log service
                var executionLog = await _backupLogService.GetLog(backupId);
            
                var logs = new BackupLogsDto
                {
                    Warnings = executionLog?.Warnings ?? new List<string>(),
                    Errors = executionLog?.Errors ?? new List<string>(),
                    ProgressLog = executionLog?.ProgressLog.Select(p => new ProgressLogEntryDto
                    {
                        Timestamp = p.Timestamp,
                        Message = p.Message,
                        PercentDone = p.PercentDone,
                        CurrentFiles = p.CurrentFile != null ? new List<string> { p.CurrentFile } : null,
                        FilesDone = (int?)p.FilesDone,
                        BytesDone = p.BytesDone
                    }).ToList() ?? new List<ProgressLogEntryDto>()
                };

            return Ok(logs);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogError(ex, "Backup not found for logs: {BackupId} with repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "null");
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
            _logger.LogError(ex, "Repository error for backup logs {BackupId} with repositoryPath={RepositoryPath}, message: {Message}", backupId, repositoryPath ?? "null", ex.Message);
            return NotFound(new ErrorResponse
            {
                Error = "Backup not found",
                Detail = $"No backup with ID {backupId} (repository does not exist)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup logs {BackupId} with repositoryPath={RepositoryPath}", backupId, repositoryPath ?? "null");
            return StatusCode(500, new ErrorResponse
            {
                Error = "Failed to get backup logs",
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
            var repositoryPath = Path.Combine(_resticOptions.RepositoryBasePath, deviceId.ToString(), shareId.ToString());
            
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
            var repositoryPath = Path.Combine(_resticOptions.RepositoryBasePath, deviceId.ToString(), shareId.ToString());

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

            // Validate target path to prevent unintended writes
            var targetPath = Path.GetFullPath(request.TargetPath);
            var allowedRestoreRoot = Path.GetFullPath("./restores");
            if (!targetPath.StartsWith(allowedRestoreRoot, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Invalid restore request",
                    Detail = "TargetPath must be within the allowed restore directory"
                });
            }

            // Determine target path - if RestoreToSource is true, we would need to get the original path
            // For now, we use the provided TargetPath in all cases
            
            // Convert IncludePaths from List to array for service call
            var includePaths = request.IncludePaths?.ToArray();

            // Construct repository path
            var repositoryPath = Path.Combine(_resticOptions.RepositoryBasePath, deviceId.ToString(), shareId.ToString());
            
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
    public ActionResult<List<FileVersion>> GetFileHistory(
        [FromQuery] Guid deviceId,
        [FromQuery] string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Invalid request",
                Detail = "filePath is required"
            });
        }

        // TODO: Implement file history tracking
        // For now return empty list
        return Ok(new List<FileVersion>());
    }

    private static string CalculateContentDedup(long dataAdded, long dataProcessed)
    {
        if (dataProcessed <= 0) return "0%";
        var contentDedup = dataProcessed > 0 ? 1.0 - ((double)dataAdded / dataProcessed) : 0.0;
        return $"{contentDedup:P1}";
    }
}
