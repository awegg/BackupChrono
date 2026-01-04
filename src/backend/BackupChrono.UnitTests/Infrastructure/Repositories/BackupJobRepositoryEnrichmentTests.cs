using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Tests for BackupJobRepository name enrichment functionality.
/// Verifies that jobs are enriched with device and share names when loaded.
/// </summary>
public class BackupJobRepositoryEnrichmentTests : IDisposable
{
    private readonly string _testRepoPath;
    private readonly Mock<ILogger<BackupJobRepository>> _mockLogger;
    private readonly Mock<IDeviceService> _mockDeviceService;
    private readonly Mock<IShareService> _mockShareService;
    private readonly BackupJobRepository _repository;

    public BackupJobRepositoryEnrichmentTests()
    {
        _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-backup-jobs-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRepoPath);

        _mockLogger = new Mock<ILogger<BackupJobRepository>>();
        _mockDeviceService = new Mock<IDeviceService>();
        _mockShareService = new Mock<IShareService>();

        _repository = new BackupJobRepository(
            _testRepoPath,
            _mockLogger.Object,
            _mockDeviceService.Object,
            _mockShareService.Object);
    }

    [Fact]
    public async Task ListJobs_EnrichesDeviceName_WhenDeviceExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = null,
            Type = BackupJobType.Manual,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow,
            DeviceName = null // Not set initially
        };
        await _repository.SaveJob(job);

        var mockDevice = new Device
        {
            Name = "TestServer",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password")
        };

        _mockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync(mockDevice);

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().Be("TestServer");
        _mockDeviceService.Verify(s => s.GetDevice(deviceId), Times.Once);
    }

    [Fact]
    public async Task ListJobs_EnrichesShareName_WhenShareExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = shareId,
            Type = BackupJobType.Manual,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow,
            DeviceName = null,
            ShareName = null // Not set initially
        };
        await _repository.SaveJob(job);

        var mockDevice = new Device
        {
            Name = "TestServer",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password")
        };

        var mockShare = new Share
        {
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/data/docs",
            Enabled = true
        };

        _mockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync(mockDevice);
        _mockShareService.Setup(s => s.GetShare(shareId))
            .ReturnsAsync(mockShare);

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().Be("TestServer");
        jobs[0].ShareName.Should().Be("Documents");
        _mockDeviceService.Verify(s => s.GetDevice(deviceId), Times.Once);
        _mockShareService.Verify(s => s.GetShare(shareId), Times.Once);
    }

    [Fact]
    public async Task ListJobs_HandlesDeletedDevice_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = null,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        await _repository.SaveJob(job);

        _mockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync((Device?)null); // Device was deleted

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().BeNull(); // Name remains null
        jobs[0].DeviceId.Should().Be(deviceId); // ID is preserved
    }

    [Fact]
    public async Task ListJobs_HandlesDeletedShare_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = shareId,
            Type = BackupJobType.Manual,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        await _repository.SaveJob(job);

        var mockDevice = new Device
        {
            Name = "TestServer",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password")
        };

        _mockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync(mockDevice);
        _mockShareService.Setup(s => s.GetShare(shareId))
            .ReturnsAsync((Share?)null); // Share was deleted

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().Be("TestServer"); // Device name enriched
        jobs[0].ShareName.Should().BeNull(); // Share name remains null
        jobs[0].ShareId.Should().Be(shareId); // ID is preserved
    }

    [Fact]
    public async Task ListJobs_PreservesExistingNames_SkipsEnrichment()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = shareId,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow,
            DeviceName = "CachedDeviceName", // Already set
            ShareName = "CachedShareName" // Already set
        };
        await _repository.SaveJob(job);

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().Be("CachedDeviceName");
        jobs[0].ShareName.Should().Be("CachedShareName");
        // Services should NOT be called since names already exist
        _mockDeviceService.Verify(s => s.GetDevice(It.IsAny<Guid>()), Times.Never);
        _mockShareService.Verify(s => s.GetShare(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ListJobs_DeviceLevelBackup_DoesNotFetchShareName()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = null, // Device-level backup
            Type = BackupJobType.Manual,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        await _repository.SaveJob(job);

        var mockDevice = new Device
        {
            Name = "TestServer",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password")
        };

        _mockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync(mockDevice);

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().Be("TestServer");
        jobs[0].ShareName.Should().BeNull();
        jobs[0].ShareId.Should().BeNull();
        // ShareService should NOT be called for device-level backups
        _mockShareService.Verify(s => s.GetShare(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ListJobs_EnrichesMultipleJobs_Correctly()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var share1Id = Guid.NewGuid();

        var job1 = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = device1Id,
            ShareId = share1Id,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-2)
        };

        var job2 = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = device2Id,
            ShareId = null,
            Type = BackupJobType.Manual,
            Status = BackupJobStatus.Running,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _repository.SaveJob(job1);
        await _repository.SaveJob(job2);

        var mockDevice1 = new Device { Name = "Server1", Protocol = ProtocolType.SMB, Host = "192.168.1.100", Port = 445, Username = "admin", Password = new EncryptedCredential("password") };
        var mockDevice2 = new Device { Name = "Server2", Protocol = ProtocolType.SSH, Host = "192.168.1.200", Port = 22, Username = "admin", Password = new EncryptedCredential("password") };
        var mockShare1 = new Share { DeviceId = device1Id, Name = "Share1", Path = "/data", Enabled = true };

        _mockDeviceService.Setup(s => s.GetDevice(device1Id)).ReturnsAsync(mockDevice1);
        _mockDeviceService.Setup(s => s.GetDevice(device2Id)).ReturnsAsync(mockDevice2);
        _mockShareService.Setup(s => s.GetShare(share1Id)).ReturnsAsync(mockShare1);

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().HaveCount(2);
        
        var enrichedJob1 = jobs.First(j => j.Id == job1.Id);
        enrichedJob1.DeviceName.Should().Be("Server1");
        enrichedJob1.ShareName.Should().Be("Share1");

        var enrichedJob2 = jobs.First(j => j.Id == job2.Id);
        enrichedJob2.DeviceName.Should().Be("Server2");
        enrichedJob2.ShareName.Should().BeNull();
    }

    [Fact]
    public async Task ListJobs_HandlesServiceException_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = null,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        await _repository.SaveJob(job);

        _mockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ThrowsAsync(new Exception("Service unavailable"));

        // Act
        var jobs = await _repository.ListJobs();

        // Assert
        jobs.Should().ContainSingle();
        jobs[0].DeviceName.Should().BeNull(); // Enrichment failed, name remains null
        jobs[0].DeviceId.Should().Be(deviceId); // ID is preserved
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRepoPath))
            {
                Directory.Delete(_testRepoPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
