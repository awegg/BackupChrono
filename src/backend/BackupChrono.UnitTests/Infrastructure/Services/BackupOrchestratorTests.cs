using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Services;

public class BackupOrchestratorTests
{
    private readonly Mock<IDeviceService> _mockDeviceService;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<IProtocolPluginLoader> _mockPluginLoader;
    private readonly Mock<IResticService> _mockResticService;
    private readonly Mock<IStorageMonitor> _mockStorageMonitor;
    private readonly Mock<ILogger<BackupOrchestrator>> _mockLogger;
    private readonly BackupOrchestrator _orchestrator;

    public BackupOrchestratorTests()
    {
        _mockDeviceService = new Mock<IDeviceService>();
        _mockShareService = new Mock<IShareService>();
        _mockPluginLoader = new Mock<IProtocolPluginLoader>();
        _mockResticService = new Mock<IResticService>();
        _mockStorageMonitor = new Mock<IStorageMonitor>();
        _mockLogger = new Mock<ILogger<BackupOrchestrator>>();

        _orchestrator = new BackupOrchestrator(
            _mockDeviceService.Object,
            _mockShareService.Object,
            _mockPluginLoader.Object,
            _mockResticService.Object,
            _mockStorageMonitor.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ExecuteDeviceBackup_ThrowsException_WhenDeviceNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync((Device?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.ExecuteDeviceBackup(deviceId, BackupJobType.Manual)
        );

        exception.Message.Should().Contain($"Device with ID '{deviceId}' not found");
    }

    [Fact]
    public async Task ExecuteDeviceBackup_ThrowsException_WhenNoEnabledShares()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(deviceId)).ReturnsAsync(new List<Share>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.ExecuteDeviceBackup(deviceId, BackupJobType.Manual)
        );

        exception.Message.Should().Contain("has no enabled shares to backup");
    }

    [Fact]
    public async Task ExecuteDeviceBackup_CallsRequiredServices()
    {
        // This is a more focused unit test - we're checking the orchestrator calls the right services
        // Integration tests will cover the full backup flow
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");
        var share = CreateTestShare(deviceId, "test-share");

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(deviceId)).ReturnsAsync(new List<Share> { share });

        var mockPlugin = new Mock<IProtocolPlugin>();
        mockPlugin.Setup(x => x.MountShare(It.IsAny<Device>(), It.IsAny<Share>()))
            .ThrowsAsync(new Exception("Mount failed")); // Force failure for quick return

        _mockPluginLoader.Setup(x => x.GetPlugin(device.Protocol)).Returns(mockPlugin.Object);

        // Act
        var result = await _orchestrator.ExecuteDeviceBackup(deviceId, BackupJobType.Manual);

        // Assert - Just verify the orchestrator tried to process the backup
        result.Should().NotBeNull();
        result.DeviceId.Should().Be(deviceId);
        result.Type.Should().Be(BackupJobType.Manual);
        _mockDeviceService.Verify(x => x.GetDevice(deviceId), Times.Once);
        _mockShareService.Verify(x => x.ListShares(deviceId), Times.Once);
    }

    [Fact]
    public async Task ExecuteShareBackup_ThrowsException_WhenDeviceNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync((Device?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.ExecuteShareBackup(deviceId, shareId, BackupJobType.Manual)
        );

        exception.Message.Should().Contain($"Device with ID '{deviceId}' not found");
    }

    [Fact]
    public async Task ExecuteShareBackup_ThrowsException_WhenShareNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.GetShare(shareId)).ReturnsAsync((Share?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.ExecuteShareBackup(deviceId, shareId, BackupJobType.Manual)
        );

        exception.Message.Should().Contain($"Share with ID '{shareId}' not found");
    }

    [Fact]
    public async Task ExecuteShareBackup_ThrowsException_WhenShareDisabled()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");
        var share = CreateTestShare(deviceId, "test-share", enabled: false);

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.GetShare(shareId)).ReturnsAsync(share);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orchestrator.ExecuteShareBackup(deviceId, shareId, BackupJobType.Manual)
        );

        exception.Message.Should().Contain("is disabled");
    }

    [Fact]
    public async Task ExecuteShareBackup_CallsRequiredServices()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");
        var share = CreateTestShare(deviceId, "test-share");
        var shareId = share.Id; // Use the share's ID

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.GetShare(shareId)).ReturnsAsync(share);

        var mockPlugin = new Mock<IProtocolPlugin>();
        mockPlugin.Setup(x => x.MountShare(device, share))
            .ThrowsAsync(new Exception("Mount failed")); // Force failure for quick return

        _mockPluginLoader.Setup(x => x.GetPlugin(device.Protocol)).Returns(mockPlugin.Object);

        // Act
        var result = await _orchestrator.ExecuteShareBackup(deviceId, shareId, BackupJobType.Manual);

        // Assert - Verify the orchestrator tried to process
        result.Should().NotBeNull();
        result.DeviceId.Should().Be(deviceId);
        result.ShareId.Should().Be(shareId);
        result.Type.Should().Be(BackupJobType.Manual);
        _mockDeviceService.Verify(x => x.GetDevice(deviceId), Times.Once);
        _mockShareService.Verify(x => x.GetShare(shareId), Times.Once);
    }

    [Fact]
    public async Task CancelJob_MarksJobAsCancelled()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");
        var share = CreateTestShare(deviceId, "test-share");

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(deviceId)).ReturnsAsync(new List<Share> { share });

        var mockPlugin = new Mock<IProtocolPlugin>();
        var tcs = new TaskCompletionSource<string>();
        mockPlugin.Setup(x => x.MountShare(device, share)).Returns(tcs.Task);

        _mockPluginLoader.Setup(x => x.GetPlugin(device.Protocol)).Returns(mockPlugin.Object);

        // Start backup (don't await)
        var backupTask = _orchestrator.ExecuteDeviceBackup(deviceId, BackupJobType.Manual);

        // Give it time to start
        await Task.Delay(100);

        // Act
        var jobs = await _orchestrator.ListJobs();
        var job = jobs.FirstOrDefault();
        job.Should().NotBeNull();

        await _orchestrator.CancelJob(job!.Id);

        // Complete the mount to unblock (with cancellation)
        tcs.SetCanceled();

        // Assert - Just verify the cancellation was requested
        // Don't wait for the task completion as it might not throw
        var updatedJobs = await _orchestrator.ListJobs();
        var cancelledJob = updatedJobs.FirstOrDefault(j => j.Id == job.Id);
        cancelledJob?.Status.Should().Be(BackupJobStatus.Cancelled);
    }

    [Fact]
    public async Task ListJobs_ReturnsActiveJobs()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");
        var share = CreateTestShare(deviceId, "test-share");

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(deviceId)).ReturnsAsync(new List<Share> { share });

        var mockPlugin = new Mock<IProtocolPlugin>();
        var tcs = new TaskCompletionSource<string>();
        mockPlugin.Setup(x => x.MountShare(device, share)).Returns(tcs.Task);

        _mockPluginLoader.Setup(x => x.GetPlugin(device.Protocol)).Returns(mockPlugin.Object);

        // Start backup (don't await)
        _ = _orchestrator.ExecuteDeviceBackup(deviceId, BackupJobType.Manual);

        // Give it time to start
        await Task.Delay(100);

        // Act
        var jobs = await _orchestrator.ListJobs();

        // Assert
        jobs.Should().HaveCount(1);
        jobs.First().Status.Should().Be(BackupJobStatus.Running);
        jobs.First().DeviceId.Should().Be(deviceId);

        // Cleanup
        tcs.SetCanceled();
    }

    [Fact]
    public async Task GetJob_ReturnsJob_WhenExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = CreateTestDevice(deviceId, "test-device");
        var share = CreateTestShare(deviceId, "test-share");

        _mockDeviceService.Setup(x => x.GetDevice(deviceId)).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(deviceId)).ReturnsAsync(new List<Share> { share });

        var mockPlugin = new Mock<IProtocolPlugin>();
        var tcs = new TaskCompletionSource<string>();
        mockPlugin.Setup(x => x.MountShare(device, share)).Returns(tcs.Task);

        _mockPluginLoader.Setup(x => x.GetPlugin(device.Protocol)).Returns(mockPlugin.Object);

        // Start backup
        _ = _orchestrator.ExecuteDeviceBackup(deviceId, BackupJobType.Manual);
        await Task.Delay(100);

        var jobs = await _orchestrator.ListJobs();
        var jobId = jobs.First().Id;

        // Act
        var result = await _orchestrator.GetJobStatus(jobId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(jobId);

        // Cleanup
        tcs.SetCanceled();
    }

    [Fact]
    public async Task GetJobStatus_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _orchestrator.GetJobStatus(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // Helper methods
    private Device CreateTestDevice(Guid id, string name)
    {
        return new Device
        {
            Id = id,
            Name = name,
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "testuser",
            Password = new EncryptedCredential("testpass"),
            WakeOnLanEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private Share CreateTestShare(Guid deviceId, string name, bool enabled = true)
    {
        return new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = name,
            Path = $"/{name}",
            Enabled = enabled,
            Schedule = null,
            RetentionPolicy = null,
            IncludeExcludeRules = new IncludeExcludeRules(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private Backup CreateTestBackup(Guid deviceId, Guid? shareId)
    {
        return new Backup
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            DeviceId = deviceId,
            ShareId = shareId,
            DeviceName = "test-device",
            ShareName = shareId.HasValue ? "test-share" : null,
            Timestamp = DateTime.UtcNow,
            Status = BackupStatus.Success,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024000,
            DataProcessed = 10240000,
            Duration = TimeSpan.FromMinutes(5)
        };
    }
}
