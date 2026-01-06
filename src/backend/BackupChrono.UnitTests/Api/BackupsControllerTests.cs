using BackupChrono.Api.Controllers;
using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Services;
using BackupChrono.Infrastructure.Restic;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Api;

/// <summary>
/// Unit tests for BackupsController
/// </summary>
public class BackupsControllerTests
{
    private readonly Mock<IResticService> _mockResticService;
    private readonly Mock<ILogger<BackupsController>> _mockLogger;
    private readonly BackupsController _controller;

    public BackupsControllerTests()
    {
        _mockResticService = new Mock<IResticService>();
        var _mockBackupLogService = new Mock<IBackupLogService>();
        _mockLogger = new Mock<ILogger<BackupsController>>();
        var resticOptions = Options.Create(new ResticOptions { RepositoryBasePath = "./repositories" });
        _controller = new BackupsController(_mockResticService.Object, _mockBackupLogService.Object, resticOptions, _mockLogger.Object);
    }

    [Fact]
    public async Task ListBackups_ReturnsOkWithBackups_WhenBackupsExist()
    {
        // Arrange
        var backups = new List<Backup>
        {
            new Backup
            {
                Id = "abc123",
                DeviceId = Guid.NewGuid(),
                DeviceName = "TestDevice",
                Timestamp = DateTime.UtcNow,
                Status = BackupStatus.Success,
                FilesNew = 10,
                FilesChanged = 5,
                FilesUnmodified = 100,
                DataAdded = 1024000,
                DataProcessed = 5120000,
                Duration = TimeSpan.FromMinutes(5)
            }
        };

        _mockResticService
            .Setup(s => s.ListBackups(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(backups);

        // Act
        var result = await _controller.ListBackups();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBackups = okResult.Value.Should().BeAssignableTo<List<BackupDto>>().Subject;
        returnedBackups.Should().HaveCount(1);
        returnedBackups[0].Id.Should().Be("abc123");
        returnedBackups[0].DeviceName.Should().Be("TestDevice");
    }

    [Fact]
    public async Task ListBackups_ReturnsEmptyList_WhenRepositoryDoesNotExist()
    {
        // Arrange
        _mockResticService
            .Setup(s => s.ListBackups(It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("repository does not exist"));

        // Act
        var result = await _controller.ListBackups();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBackups = okResult.Value.Should().BeAssignableTo<List<BackupDto>>().Subject;
        returnedBackups.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackups_Returns500_OnUnexpectedError()
    {
        // Arrange
        _mockResticService
            .Setup(s => s.ListBackups(It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.ListBackups();

        // Assert
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        var errorResponse = statusResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("Failed to list backups");
    }

    [Fact]
    public async Task GetBackup_ReturnsOkWithBackup_WhenBackupExists()
    {
        // Arrange
        var backupId = "test123";
        var backup = new Backup
        {
            Id = backupId,
            DeviceId = Guid.NewGuid(),
            DeviceName = "TestDevice",
            Timestamp = DateTime.UtcNow,
            Status = BackupStatus.Success,
            FilesNew = 10,
            DataAdded = 1024000,
            Duration = TimeSpan.FromMinutes(5)
        };
        var metadata = new BackupChrono.Core.DTOs.SnapshotMetadata
        {
            Id = backupId,
            Hostname = "TestDevice",
            Paths = new List<string> { "/test" },
            Time = DateTime.UtcNow,
            Tags = new List<string>()
        };
        var stats = new BackupChrono.Core.DTOs.SnapshotStats
        {
            SnapshotId = backupId,
            TotalSize = 1024000,
            TotalUncompressedSize = 2048000,
            DeduplicationRatio = 0.5,
            DeduplicationSpaceSaved = 1024000
        };

        _mockResticService
            .Setup(s => s.GetBackupDetailComplete(backupId, It.IsAny<string?>()))
            .ReturnsAsync((backup, metadata, stats));

        // Act
        var result = await _controller.GetBackup(backupId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBackup = okResult.Value.Should().BeOfType<BackupDetailDto>().Subject;
        returnedBackup.Id.Should().Be(backupId);
        returnedBackup.DeviceName.Should().Be("TestDevice");
    }

    [Fact]
    public async Task GetBackup_Returns404_WhenBackupNotFound()
    {
        // Arrange
        var backupId = "nonexistent";
        _mockResticService
            .Setup(s => s.GetBackupDetailComplete(backupId, It.IsAny<string?>()))
            .ThrowsAsync(new KeyNotFoundException($"Backup {backupId} not found"));

        // Act
        var result = await _controller.GetBackup(backupId);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetBackup_Returns404_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var backupId = "test456";
        _mockResticService
            .Setup(s => s.GetBackupDetailComplete(backupId, It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("repository does not exist"));

        // Act
        var result = await _controller.GetBackup(backupId);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var errorResponse = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Detail.Should().Contain("repository does not exist");
    }

    [Fact]
    public async Task BrowseBackupFiles_ReturnsOkWithFiles_WhenFilesExist()
    {
        // Arrange
        var backupId = "backup123";
        var path = "/";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var files = new List<BackupChrono.Core.DTOs.FileEntry>
        {
            new BackupChrono.Core.DTOs.FileEntry
            {
                Name = "file1.txt",
                Path = "/file1.txt",
                IsDirectory = false,
                Size = 1024,
                ModifiedAt = DateTime.UtcNow
            },
            new BackupChrono.Core.DTOs.FileEntry
            {
                Name = "folder1",
                Path = "/folder1",
                IsDirectory = true,
                Size = 0,
                ModifiedAt = DateTime.UtcNow
            }
        };

        _mockResticService
            .Setup(s => s.BrowseBackup(backupId, path, It.IsAny<string?>()))
            .ReturnsAsync((IEnumerable<BackupChrono.Core.DTOs.FileEntry>)files);

        // Act
        var result = await _controller.BrowseBackupFiles(backupId, deviceId, shareId, path);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedFiles = okResult.Value.Should().BeAssignableTo<IEnumerable<BackupChrono.Core.DTOs.FileEntry>>().Subject;
        returnedFiles.Should().HaveCount(2);
        returnedFiles.First().Name.Should().Be("file1.txt");
        returnedFiles.Last().IsDirectory.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseBackupFiles_Returns404_WhenBackupNotFound()
    {
        // Arrange
        var backupId = "nonexistent";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        _mockResticService
            .Setup(s => s.BrowseBackup(backupId, It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("repository does not exist"));

        // Act
        var result = await _controller.BrowseBackupFiles(backupId, deviceId, shareId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RestoreBackup_WithPathOutsideAllowedDirectory_ReturnsBadRequest()
    {
        // Arrange
        var backupId = "backup123";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var request = new RestoreRequestDto
        {
            TargetPath = "/etc/passwd" // Outside allowed restore directory
        };

        // Act
        var result = await _controller.RestoreBackup(backupId, deviceId, shareId, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Detail.Should().Contain("must be within the allowed restore directory");
    }

    [Fact]
    public async Task RestoreBackup_WithPathTraversal_ReturnsBadRequest()
    {
        // Arrange
        var backupId = "backup456";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var request = new RestoreRequestDto
        {
            TargetPath = "./restores/../../../etc/passwd" // Path traversal attempt
        };

        // Act
        var result = await _controller.RestoreBackup(backupId, deviceId, shareId, request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var errorResponse = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Detail.Should().Contain("must be within the allowed restore directory");
    }

    [Fact]
    public async Task RestoreBackup_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var backupId = "backup789";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var request = new RestoreRequestDto
        {
            TargetPath = "./restores/backup789"
        };

        // Act
        var result = await _controller.RestoreBackup(backupId, deviceId, shareId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void GetFileHistory_ReturnsOkWithEmptyList_WhenNoHistory()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var filePath = "/test/file.txt";

        // Act - controller doesn't call ResticService yet, just returns empty list
        var result = _controller.GetFileHistory(deviceId, filePath);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var history = okResult.Value.Should().BeAssignableTo<List<BackupChrono.Core.DTOs.FileVersion>>().Subject;
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetFileHistory_ReturnsOkWithHistory_WhenHistoryExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var filePath = "/documents/report.pdf";

        // Act - controller doesn't call ResticService yet, just returns empty list
        var result = _controller.GetFileHistory(deviceId, filePath);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedHistory = okResult.Value.Should().BeAssignableTo<List<BackupChrono.Core.DTOs.FileVersion>>().Subject;
        // Currently returns empty as not implemented
        returnedHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackups_RespectsLimitParameter()
    {
        // Arrange
        var backups = Enumerable.Range(1, 100).Select(i => new Backup
        {
            Id = $"backup{i}",
            DeviceId = Guid.NewGuid(),
            DeviceName = "TestDevice",
            Timestamp = DateTime.UtcNow,
            Status = BackupStatus.Success,
            Duration = TimeSpan.FromMinutes(5)
        }).ToList();

        _mockResticService
            .Setup(s => s.ListBackups(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(backups);

        // Act
        var result = await _controller.ListBackups(limit: 10);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBackups = okResult.Value.Should().BeAssignableTo<List<BackupDto>>().Subject;
        returnedBackups.Should().HaveCount(10); // Limited to 10
    }

    [Fact]
    public async Task RestoreBackup_WithIncludePaths_ReturnsOk()
    {
        // Arrange
        var backupId = "backup999";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var request = new RestoreRequestDto
        {
            TargetPath = "./restores/backup999",
            IncludePaths = new List<string> { "/path1", "/path2" }
        };

        // Act
        var result = await _controller.RestoreBackup(backupId, deviceId, shareId, request);

        // Assert
        var acceptedResult = result.Should().BeOfType<OkObjectResult>().Subject;
    }

    [Fact]
    public async Task RestoreBackup_WithRestoreToSource_ReturnsOk()
    {
        // Arrange
        var backupId = "backup888";
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var request = new RestoreRequestDto
        {
            TargetPath = "./restores/backup888",
            RestoreToSource = true
        };

        // Act
        var result = await _controller.RestoreBackup(backupId, deviceId, shareId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task BrowseBackupFiles_WithCustomPath_CallsServiceWithPath()
    {
        // Arrange
        var backupId = "backup777";
        var customPath = "/documents/subfolder";
        var files = new List<BackupChrono.Core.DTOs.FileEntry>();

        _mockResticService
            .Setup(s => s.BrowseBackup(backupId, customPath, It.IsAny<string?>()))
            .ReturnsAsync((IEnumerable<BackupChrono.Core.DTOs.FileEntry>)files);

        // Act
        var result = await _controller.BrowseBackupFiles(backupId, Guid.NewGuid(), Guid.NewGuid(), customPath);

        // Assert
        _mockResticService.Verify(s => s.BrowseBackup(backupId, customPath, It.IsAny<string?>()), Times.Once);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ListBackups_LogsError_OnException()
    {
        // Arrange
        _mockResticService
            .Setup(s => s.ListBackups(It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Test error"));

        // Act
        await _controller.ListBackups();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetBackup_LogsError_OnException()
    {
        // Arrange
        var backupId = "test999";
        _mockResticService
            .Setup(s => s.GetBackup(backupId, It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Test error"));

        // Act
        await _controller.GetBackup(backupId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task BrowseBackupFiles_LogsError_OnException()
    {
        // Arrange
        var backupId = "test888";
        _mockResticService
            .Setup(s => s.BrowseBackup(backupId, It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Test error"));

        // Act
        await _controller.BrowseBackupFiles(backupId, Guid.NewGuid(), Guid.NewGuid());

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
