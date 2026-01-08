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
            RepositoryBasePath = Path.Combine(Path.GetTempPath(), "test-repositories")
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
        var requestedShare = new Share { Id = shareId, DeviceId = deviceId, Name = "RequestedShare", Path = "/requested", Enabled = true };
        var otherShare = new Share { Id = otherShareId, DeviceId = deviceId, Name = "OtherShare", Path = "/other", Enabled = true };

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
            .ReturnsAsync(new List<Share> { requestedShare, otherShare });

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string? hostname, string repositoryPath) =>
            {
                if (repositoryPath.Contains(otherShareId.ToString()))
                    return new List<Backup> { backupForOtherShare };
                return new List<Backup>();
            });

        // Act
        var result = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result.Should().BeNull("no backup exists for this share");
    }

    [Fact]
    public async Task GetLatestBackupsForShares_ReturnsBatchResults()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var share1Id = Guid.NewGuid();
        var share2Id = Guid.NewGuid();
        var share3Id = Guid.NewGuid();

        var device = CreateTestDevice(deviceId);
        var share1 = new Share { Id = share1Id, DeviceId = deviceId, Name = "Share1", Path = "/share1", Enabled = true };
        var share2 = new Share { Id = share2Id, DeviceId = deviceId, Name = "Share2", Path = "/share2", Enabled = true };
        var share3 = new Share { Id = share3Id, DeviceId = deviceId, Name = "Share3", Path = "/share3", Enabled = true };

        var backup1 = new Backup
        {
            Id = "backup1",
            DeviceId = deviceId,
            ShareId = share1Id,
            DeviceName = "TestDevice",
            ShareName = "Share1",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Status = BackupStatus.Success,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024 * 100,
            DataProcessed = 1024 * 1024 * 500,
            Duration = TimeSpan.FromMinutes(5)
        };

        var backup2 = new Backup
        {
            Id = "backup2",
            DeviceId = deviceId,
            ShareId = share2Id,
            DeviceName = "TestDevice",
            ShareName = "Share2",
            Timestamp = DateTime.UtcNow.AddHours(-2),
            Status = BackupStatus.Success,
            FilesNew = 20,
            FilesChanged = 10,
            FilesUnmodified = 200,
            DataAdded = 1024 * 1024 * 200,
            DataProcessed = 1024 * 1024 * 1000,
            Duration = TimeSpan.FromMinutes(10)
        };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        _mockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share1, share2, share3 });

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string? hostname, string repositoryPath) =>
            {
                if (repositoryPath.Contains(share1Id.ToString()))
                    return new List<Backup> { backup1 };
                if (repositoryPath.Contains(share2Id.ToString()))
                    return new List<Backup> { backup2 };
                return new List<Backup>();
            });

        // Act
        var result = await _repository.GetLatestBackupsForShares(new List<Guid> { share1Id, share2Id, share3Id });

        // Assert
        result.Should().HaveCount(2); // share3 has no backups
        result[share1Id].Should().Be(backup1);
        result[share2Id].Should().Be(backup2);
        result.Should().NotContainKey(share3Id);
    }

    [Fact]
    public async Task GetOverviewStatistics_CalculatesCorrectStatistics()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var share1Id = Guid.NewGuid();
        var share2Id = Guid.NewGuid();
        var share3Id = Guid.NewGuid();

        var device1 = CreateTestDevice(device1Id, "Device1");
        var device2 = CreateTestDevice(device2Id, "Device2");

        var share1 = new Share { Id = share1Id, DeviceId = device1Id, Name = "Share1", Path = "/share1", Enabled = true };
        var share2 = new Share { Id = share2Id, DeviceId = device1Id, Name = "Share2", Path = "/share2", Enabled = true };
        var share3 = new Share { Id = share3Id, DeviceId = device2Id, Name = "Share3", Path = "/share3", Enabled = true };

        var recentBackup = new Backup
        {
            Id = "backup1",
            DeviceId = device1Id,
            ShareId = share1Id,
            DeviceName = "Device1",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Status = BackupStatus.Success,
            FilesNew = 100,
            FilesChanged = 50,
            FilesUnmodified = 1000,
            DataAdded = 1024L * 1024 * 1024 * 10, // 10 GB
            DataProcessed = 1024L * 1024 * 1024 * 50,
            Duration = TimeSpan.FromMinutes(30)
        };

        var failedBackup = new Backup
        {
            Id = "backup2",
            DeviceId = device1Id,
            ShareId = share2Id,
            DeviceName = "Device1",
            Timestamp = DateTime.UtcNow.AddHours(-2),
            Status = BackupStatus.Failed,
            FilesNew = 50,
            FilesChanged = 25,
            FilesUnmodified = 500,
            DataAdded = 1024L * 1024 * 1024 * 5, // 5 GB
            DataProcessed = 1024L * 1024 * 1024 * 20,
            Duration = TimeSpan.FromMinutes(15)
        };

        var staleBackup = new Backup
        {
            Id = "backup3",
            DeviceId = device2Id,
            ShareId = share3Id,
            DeviceName = "Device2",
            Timestamp = DateTime.UtcNow.AddDays(-3),
            Status = BackupStatus.Success,
            FilesNew = 200,
            FilesChanged = 100,
            FilesUnmodified = 2000,
            DataAdded = 1024L * 1024 * 1024 * 20, // 20 GB
            DataProcessed = 1024L * 1024 * 1024 * 100,
            Duration = TimeSpan.FromMinutes(60)
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

        _mockResticService
            .Setup(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string? hostname, string repositoryPath) =>
            {
                if (repositoryPath.Contains(share1Id.ToString()))
                    return new List<Backup> { recentBackup };
                if (repositoryPath.Contains(share2Id.ToString()))
                    return new List<Backup> { failedBackup };
                if (repositoryPath.Contains(share3Id.ToString()))
                    return new List<Backup> { staleBackup };
                return new List<Backup>();
            });

        // Act
        var result = await _repository.GetOverviewStatistics();

        // Assert
        result.TotalDevices.Should().Be(2);
        result.TotalShares.Should().Be(3);
        result.TotalFiles.Should().Be(1150 + 575 + 2300); // Sum of all files from all backups
        result.TotalProtectedBytes.Should().Be(1024L * 1024 * 1024 * 35); // 35 GB total
        result.DevicesWithFailures.Should().Be(1); // Only device1 has failures
        result.DevicesWithStaleBackups.Should().Be(1); // Only device2 has stale backups
    }

    [Fact]
    public async Task GetLatestBackup_ReturnsCachedValue_WhenCacheIsValid()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId);
        var share = new Share { Id = shareId, DeviceId = deviceId, Name = "Share1", Path = "/share1", Enabled = true };

        var backup = new Backup
        {
            Id = "backup1",
            DeviceId = deviceId,
            ShareId = shareId,
            DeviceName = "TestDevice",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Status = BackupStatus.Success,
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
            .ReturnsAsync(new List<Backup> { backup });

        // Act - First call should populate cache
        var result1 = await _repository.GetLatestBackup(deviceId, shareId);
        
        // Second call should use cache (restic service should only be called once)
        var result2 = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(backup.Id);
        result2!.Id.Should().Be(backup.Id);
        
        // Verify ListBackups was called only once (cache hit on second call)
        _mockResticService.Verify(x => x.ListBackups(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetLatestBackup_ReturnsNull_WhenExceptionOccurs()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _repository.GetLatestBackup(deviceId, shareId);

        // Assert
        result.Should().BeNull("exception should be handled gracefully");
    }

    [Fact]
    public async Task GetLatestBackupsForShares_ReturnsEmptyDictionary_WhenExceptionOccurs()
    {
        // Arrange
        var shareIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _repository.GetLatestBackupsForShares(shareIds);

        // Assert
        result.Should().BeEmpty("exception should be handled gracefully");
    }

    [Fact]
    public async Task GetOverviewStatistics_ReturnsDefaultStatistics_WhenExceptionOccurs()
    {
        // Arrange
        _mockDeviceService
            .Setup(x => x.ListDevices())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _repository.GetOverviewStatistics();

        // Assert
        result.Should().NotBeNull();
        result.TotalDevices.Should().Be(0);
        result.TotalShares.Should().Be(0);
    }
}
