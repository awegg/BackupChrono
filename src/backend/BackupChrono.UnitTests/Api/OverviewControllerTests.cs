using BackupChrono.Api.Controllers;
using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Api;

/// <summary>
/// Unit tests for OverviewController
/// </summary>
public class OverviewControllerTests
{
    private readonly Mock<IBackupRepository> _mockBackupRepository;
    private readonly Mock<IDeviceService> _mockDeviceService;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<ILogger<OverviewController>> _mockLogger;
    private readonly OverviewController _controller;

    public OverviewControllerTests()
    {
        _mockBackupRepository = new Mock<IBackupRepository>();
        _mockDeviceService = new Mock<IDeviceService>();
        _mockShareService = new Mock<IShareService>();
        _mockLogger = new Mock<ILogger<OverviewController>>();
        
        _controller = new OverviewController(
            _mockBackupRepository.Object,
            _mockDeviceService.Object,
            _mockShareService.Object,
            _mockLogger.Object
        );
    }

    private Device CreateTestDevice(Guid? id = null, string name = "TestDevice")
    {
        return new Device
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Protocol = ProtocolType.SMB,
            Host = "test-host",
            Username = "testuser",
            Password = new EncryptedCredential("testpassword")
        };
    }

    private Share CreateTestShare(Guid deviceId, Guid? id = null, string name = "TestShare", bool enabled = true)
    {
        return new Share
        {
            Id = id ?? Guid.NewGuid(),
            DeviceId = deviceId,
            Name = name,
            Path = "/data",
            Enabled = enabled
        };
    }

    private Backup CreateTestBackup(Guid deviceId, Guid shareId, DateTime timestamp, BackupStatus status = BackupStatus.Success)
    {
        return new Backup
        {
            Id = "backup123",
            DeviceId = deviceId,
            ShareId = shareId,
            DeviceName = "TestDevice",
            ShareName = "TestShare",
            Timestamp = timestamp,
            Status = status,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024 * 1024, // 1 GB
            DataProcessed = 1024 * 1024 * 1024,
            Duration = TimeSpan.FromMinutes(5)
        };
    }

    [Fact]
    public async Task GetOverview_ReturnsEmptyOverview_WhenNoDevicesExist()
    {
        // Arrange
        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device>());

        // Act
        var result = await _controller.GetOverview();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;
        
        overview.DevicesNeedingAttention.Should().Be(0);
        overview.TotalProtectedDataTB.Should().Be(0);
        overview.RecentFailures.Should().Be(0);
        overview.Devices.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverview_ReturnsDeviceWithSuccessStatus_WhenBackupIsRecent()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "Server");
        var share = CreateTestShare(deviceId, shareId, "Documents");
        var recentBackup = CreateTestBackup(deviceId, shareId, DateTime.UtcNow.AddHours(-1));

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, shareId))
            .ReturnsAsync(recentBackup);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices.Should().HaveCount(1);
        overview.Devices[0].Name.Should().Be("Server");
        overview.Devices[0].Status.Should().Be("Success");
        overview.Devices[0].Shares.Should().HaveCount(1);
        overview.Devices[0].Shares[0].Status.Should().Be("Success");
        overview.DevicesNeedingAttention.Should().Be(0);
    }

    [Fact]
    public async Task GetOverview_ReturnsWarningStatus_WhenNoBackupExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId);
        var share = CreateTestShare(deviceId, shareId);

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, shareId))
            .ReturnsAsync((Backup?)null);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices[0].Status.Should().Be("Warning");
        overview.Devices[0].Shares[0].Status.Should().Be("Warning");
        overview.DevicesNeedingAttention.Should().Be(1);
    }

    [Fact]
    public async Task GetOverview_ReturnsWarningStatus_WhenBackupIsStale()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId);
        var share = CreateTestShare(deviceId, shareId);
        var staleBackup = CreateTestBackup(deviceId, shareId, DateTime.UtcNow.AddDays(-3));

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, shareId))
            .ReturnsAsync(staleBackup);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices[0].Status.Should().Be("Warning");
        overview.Devices[0].Shares[0].Status.Should().Be("Warning");
        overview.Devices[0].Shares[0].IsStale.Should().BeTrue();
        overview.DevicesNeedingAttention.Should().Be(1);
    }

    [Fact]
    public async Task GetOverview_ReturnsFailedStatus_WhenBackupFailed()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId);
        var share = CreateTestShare(deviceId, shareId);
        var failedBackup = CreateTestBackup(deviceId, shareId, DateTime.UtcNow.AddHours(-1), BackupStatus.Failed);

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, shareId))
            .ReturnsAsync(failedBackup);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices[0].Status.Should().Be("Failed");
        overview.Devices[0].Shares[0].Status.Should().Be("Failed");
        overview.DevicesNeedingAttention.Should().Be(1);
        overview.RecentFailures.Should().Be(1);
    }

    [Fact]
    public async Task GetOverview_ReturnsDisabledStatus_WhenShareDisabled()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId);
        var share = CreateTestShare(deviceId, shareId, enabled: false);

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, shareId))
            .ReturnsAsync((Backup?)null);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices[0].Status.Should().Be("Disabled");
        overview.Devices[0].Shares[0].Status.Should().Be("Disabled");
        overview.DevicesNeedingAttention.Should().Be(0);
    }

    [Fact]
    public async Task GetOverview_CalculatesCorrectMetrics_WithMultipleDevicesAndShares()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var share1Id = Guid.NewGuid();
        var share2Id = Guid.NewGuid();
        var share3Id = Guid.NewGuid();

        var device1 = CreateTestDevice(device1Id, "Server1");
        var device2 = CreateTestDevice(device2Id, "Server2");

        var share1 = CreateTestShare(device1Id, share1Id, "Documents");
        var share2 = CreateTestShare(device1Id, share2Id, "Photos");
        var share3 = CreateTestShare(device2Id, share3Id, "Videos");

        var backup1 = new Backup
        {
            Id = "backup1",
            DeviceId = device1Id,
            ShareId = share1Id,
            DeviceName = "Server1",
            ShareName = "Documents",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Status = BackupStatus.Success,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024L * 1024 * 1024 * 500, // 500 GB
            DataProcessed = 1024L * 1024 * 1024 * 500,
            Duration = TimeSpan.FromMinutes(5)
        };
        var backup2 = new Backup
        {
            Id = "backup2",
            DeviceId = device1Id,
            ShareId = share2Id,
            DeviceName = "Server1",
            ShareName = "Photos",
            Timestamp = DateTime.UtcNow.AddHours(-2),
            Status = BackupStatus.Success,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024L * 1024 * 1024 * 300, // 300 GB
            DataProcessed = 1024L * 1024 * 1024 * 300,
            Duration = TimeSpan.FromMinutes(5)
        };
        var backup3 = new Backup
        {
            Id = "backup3",
            DeviceId = device2Id,
            ShareId = share3Id,
            DeviceName = "Server2",
            ShareName = "Videos",
            Timestamp = DateTime.UtcNow.AddHours(-3),
            Status = BackupStatus.Success,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024L * 1024 * 1024 * 200, // 200 GB
            DataProcessed = 1024L * 1024 * 1024 * 200,
            Duration = TimeSpan.FromMinutes(5)
        };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device1, device2 });

        _mockShareService
            .Setup(x => x.ListShares(device1Id))
            .ReturnsAsync(new List<Share> { share1, share2 });

        _mockShareService
            .Setup(x => x.ListShares(device2Id))
            .ReturnsAsync(new List<Share> { share3 });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(device1Id, share1Id))
            .ReturnsAsync(backup1);

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(device1Id, share2Id))
            .ReturnsAsync(backup2);

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(device2Id, share3Id))
            .ReturnsAsync(backup3);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices.Should().HaveCount(2);
        overview.TotalProtectedDataTB.Should().BeApproximately(0.98, 0.01); // ~1000GB = ~0.98TB
        overview.DevicesNeedingAttention.Should().Be(0);
    }

    [Fact]
    public async Task GetOverview_ReturnsPartialStatus_WhenBackupPartial()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId);
        var share = CreateTestShare(deviceId, shareId);
        var partialBackup = CreateTestBackup(deviceId, shareId, DateTime.UtcNow.AddHours(-1), BackupStatus.Partial);

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, shareId))
            .ReturnsAsync(partialBackup);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices[0].Status.Should().Be("Partial");
        overview.Devices[0].Shares[0].Status.Should().Be("Partial");
    }

    [Fact]
    public async Task GetOverview_CountsRecentFailuresCorrectly()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var share1Id = Guid.NewGuid();
        var share2Id = Guid.NewGuid();

        var device1 = CreateTestDevice(device1Id);
        var device2 = CreateTestDevice(device2Id);
        var share1 = CreateTestShare(device1Id, share1Id);
        var share2 = CreateTestShare(device2Id, share2Id);

        // Recent failure (within 24 hours)
        var recentFailure = CreateTestBackup(device1Id, share1Id, DateTime.UtcNow.AddHours(-12), BackupStatus.Failed);
        
        // Old failure (more than 24 hours ago)
        var oldFailure = CreateTestBackup(device2Id, share2Id, DateTime.UtcNow.AddDays(-2), BackupStatus.Failed);

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device1, device2 });

        _mockShareService
            .Setup(x => x.ListShares(device1Id))
            .ReturnsAsync(new List<Share> { share1 });

        _mockShareService
            .Setup(x => x.ListShares(device2Id))
            .ReturnsAsync(new List<Share> { share2 });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(device1Id, share1Id))
            .ReturnsAsync(recentFailure);

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(device2Id, share2Id))
            .ReturnsAsync(oldFailure);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.RecentFailures.Should().Be(1); // Only the recent failure should count
    }

    [Fact]
    public async Task GetOverview_PrioritizesFailedOverWarning_InDeviceStatus()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var share1Id = Guid.NewGuid();
        var share2Id = Guid.NewGuid();

        var device = CreateTestDevice(deviceId);
        var share1 = CreateTestShare(deviceId, share1Id, "Share1");
        var share2 = CreateTestShare(deviceId, share2Id, "Share2");

        var failedBackup = CreateTestBackup(deviceId, share1Id, DateTime.UtcNow.AddHours(-1), BackupStatus.Failed);
        var warningBackup = CreateTestBackup(deviceId, share2Id, DateTime.UtcNow.AddDays(-3), BackupStatus.Success);

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share1, share2 });

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, share1Id))
            .ReturnsAsync(failedBackup);

        _mockBackupRepository
            .Setup(x => x.GetLatestBackup(deviceId, share2Id))
            .ReturnsAsync(warningBackup);

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var overview = okResult.Value.Should().BeOfType<BackupOverviewDto>().Subject;

        overview.Devices[0].Status.Should().Be("Failed"); // Failed should take priority
    }

    [Fact]
    public async Task GetOverview_Returns500_WhenExceptionOccurs()
    {
        // Arrange
        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetOverview();

        // Assert
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        
        var errorResponse = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        errorResponse.Error.Should().Be("Failed to fetch backup overview");
        errorResponse.Detail.Should().Be("Database error");
    }
}
