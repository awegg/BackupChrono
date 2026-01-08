using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Repositories;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BackupChrono.UnitTests.Repositories;

public class BackupRepositoryTests
{
    private readonly Mock<IResticService> _mockResticService;
    private readonly Mock<IDeviceService> _mockDeviceService;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<ILogger<BackupRepository>> _mockLogger;
    private readonly BackupRepository _repository;

    public BackupRepositoryTests()
    {
        _mockResticService = new Mock<IResticService>();
        _mockDeviceService = new Mock<IDeviceService>();
        _mockShareService = new Mock<IShareService>();
        _mockLogger = new Mock<ILogger<BackupRepository>>();
        
        var resticOptions = Options.Create(new ResticOptions
        {
            RepositoryBasePath = "C:\\test-repositories"
        });
        
        _repository = new BackupRepository(
            _mockResticService.Object,
            _mockDeviceService.Object,
            _mockShareService.Object,
            resticOptions,
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

    [Fact]
    public async Task GetLatestBackup_WithDeviceLevelBackup_ShouldReturnBackupForShare()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var sharePath = "/data/documents";
        var backupTimestamp = DateTime.UtcNow.AddHours(-1);

        var device = CreateTestDevice(deviceId);

        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = sharePath,
            Enabled = true
        };

        var deviceLevelBackup = new Backup
        {
            Id = "backup123",
            DeviceId = deviceId,
            ShareId = null, // Device-level backup
            DeviceName = "TestDevice",
            ShareName = null,
            Timestamp = backupTimestamp,
            Status = BackupStatus.Success,
            SharesPaths = new Dictionary<string, string>
            {
                { "Documents", sharePath }
            },
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024 * 100, // 100 MB
            DataProcessed = 1024 * 1024 * 500,
            Duration = TimeSpan.FromMinutes(5)
        };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Backup> { deviceLevelBackup });

        // Act
        var result = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result.Should().NotBeNull("device-level backup should be mapped to share");
        result!.Timestamp.Should().Be(backupTimestamp);
        result.DeviceId.Should().Be(deviceId);
    }

    [Fact]
    public async Task GetLatestBackup_WithShareLevelBackup_ShouldReturnBackup()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var backupTimestamp = DateTime.UtcNow.AddHours(-1);

        var device = CreateTestDevice(deviceId);
        
        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/data/documents",
            Enabled = true
        };

        var shareLevelBackup = new Backup
        {
            Id = "backup456",
            DeviceId = deviceId,
            ShareId = shareId, // Share-level backup
            DeviceName = "TestDevice",
            ShareName = "Documents",
            Timestamp = backupTimestamp,
            Status = BackupStatus.Success,
            SharesPaths = new Dictionary<string, string>
            {
                { "Documents", "/data/documents" }
            },
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024 * 100,
            DataProcessed = 1024 * 1024 * 500,
            Duration = TimeSpan.FromMinutes(5)
        };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Backup> { shareLevelBackup });

        // Act
        var result = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(backupTimestamp);
        result.ShareId.Should().Be(shareId);
    }

    [Fact]
    public async Task GetLatestBackup_WithBothDeviceAndShareLevelBackups_ShouldReturnNewest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var sharePath = "/data/documents";
        var olderTimestamp = DateTime.UtcNow.AddHours(-2);
        var newerTimestamp = DateTime.UtcNow.AddHours(-1);

        var device = CreateTestDevice(deviceId);

        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = sharePath,
            Enabled = true
        };

        var deviceLevelBackup = new Backup
        {
            Id = "backup123",
            DeviceId = deviceId,
            ShareId = null,
            DeviceName = "TestDevice",
            ShareName = null,
            Timestamp = newerTimestamp,
            Status = BackupStatus.Success,
            SharesPaths = new Dictionary<string, string>
            {
                { "Documents", sharePath }
            },
            FilesNew = 15,
            FilesChanged = 3,
            FilesUnmodified = 120,
            DataAdded = 1024 * 1024 * 150,
            DataProcessed = 1024 * 1024 * 600,
            Duration = TimeSpan.FromMinutes(6)
        };

        var shareLevelBackup = new Backup
        {
            Id = "backup456",
            DeviceId = deviceId,
            ShareId = shareId,
            DeviceName = "TestDevice",
            ShareName = "Documents",
            Timestamp = olderTimestamp,
            Status = BackupStatus.Success,
            SharesPaths = new Dictionary<string, string>
            {
                { "Documents", sharePath }
            },
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024 * 100,
            DataProcessed = 1024 * 1024 * 500,
            Duration = TimeSpan.FromMinutes(5)
        };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Backup> { deviceLevelBackup, shareLevelBackup });

        // Act
        var result = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(newerTimestamp, "should return the newer backup");
        result.Id.Should().Be("backup123");
    }

    [Fact]
    public async Task GetLatestBackup_WithNoMatchingBackup_ShouldReturnNull()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var otherShareId = Guid.NewGuid();

        var device = CreateTestDevice(deviceId);

        var backupForOtherShare = new Backup
        {
            Id = "backup789",
            DeviceId = deviceId,
            ShareId = otherShareId,
            DeviceName = "TestDevice",
            ShareName = "OtherShare",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Status = BackupStatus.Success,
            SharesPaths = new Dictionary<string, string>(),
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024 * 100,
            DataProcessed = 1024 * 1024 * 500,
            Duration = TimeSpan.FromMinutes(5)
        };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share>());

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Backup> { backupForOtherShare });

        // Act
        var result = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result.Should().BeNull("no backup exists for this share");
    }
}
