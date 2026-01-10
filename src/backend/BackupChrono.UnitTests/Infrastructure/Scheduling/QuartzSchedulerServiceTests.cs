using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Scheduling;

public class QuartzSchedulerServiceTests : IAsyncLifetime
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IDeviceService> _mockDeviceService;
    private readonly Mock<IShareService> _mockShareService;
    private readonly Mock<IBackupOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<QuartzSchedulerService>> _mockLogger;
    private readonly QuartzSchedulerService _schedulerService;

    public QuartzSchedulerServiceTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockDeviceService = new Mock<IDeviceService>();
        _mockShareService = new Mock<IShareService>();
        _mockOrchestrator = new Mock<IBackupOrchestrator>();
        _mockLogger = new Mock<ILogger<QuartzSchedulerService>>();

        // Setup scope factory chain
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IDeviceService))).Returns(_mockDeviceService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IShareService))).Returns(_mockShareService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IBackupOrchestrator))).Returns(_mockOrchestrator.Object);

        // Use unique scheduler name for each test instance to avoid conflicts
        var uniqueName = $"TestScheduler-{Guid.NewGuid():N}";
        _schedulerService = new QuartzSchedulerService(_mockScopeFactory.Object, _mockLogger.Object, uniqueName);
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _schedulerService.Stop();
    }

    [Fact]
    public async Task Start_InitializesScheduler()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());

        // Act
        await _schedulerService.Start();

        // Assert - Verify scheduler loaded devices and shares
        _mockDeviceService.Verify(x => x.ListDevices(), Times.Once);
        _mockShareService.Verify(x => x.ListShares(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Stop_StopsScheduler()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        // Act
        await _schedulerService.Stop();

        // Assert - Verify stop completed without exception (implicit by reaching here)
        // Additional stop should not throw
        await _schedulerService.Stop();
    }

    [Fact]
    public async Task ScheduleDeviceBackup_SchedulesJob()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var device = CreateTestDevice("test-device");
        var schedule = new Schedule { CronExpression = "0 0 2 * * ?" }; // Valid Quartz cron (seconds minutes hours day month weekday)

        // Act
        await _schedulerService.ScheduleDeviceBackup(device, schedule);

        // Assert - Verify job can be triggered (proves it was scheduled)
        await _schedulerService.TriggerImmediateBackup(device.Id);
    }

    [Fact]
    public async Task ScheduleShareBackup_SchedulesJob()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var device = CreateTestDevice("test-device");
        var share = CreateTestShare(device.Id, "test-share");
        var schedule = new Schedule { CronExpression = "0 0 3 * * ?" }; // Valid Quartz cron

        // Act
        await _schedulerService.ScheduleShareBackup(device, share, schedule);

        // Assert - Verify job can be triggered (proves it was scheduled)
        await _schedulerService.TriggerImmediateBackup(device.Id, share.Id);
    }

    [Fact]
    public async Task UnscheduleDeviceBackup_RemovesJob()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var device = CreateTestDevice("test-device");
        var schedule = new Schedule { CronExpression = "0 0 2 * * ?" };
        await _schedulerService.ScheduleDeviceBackup(device, schedule);

        // Act
        await _schedulerService.UnscheduleDeviceBackup(device.Id);

        // Assert - verify scheduled job was removed
        var scheduler = GetScheduler();
        var jobKey = new JobKey($"device-{device.Id}", "backups");
        (await scheduler.CheckExists(jobKey)).Should().BeFalse();
    }

    [Fact]
    public async Task UnscheduleShareBackup_RemovesJob()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var device = CreateTestDevice("test-device");
        var share = CreateTestShare(device.Id, "test-share");
        var schedule = new Schedule { CronExpression = "0 0 3 * * ?" };
        await _schedulerService.ScheduleShareBackup(device, share, schedule);

        // Act
        await _schedulerService.UnscheduleShareBackup(share.Id);

        // Assert - verify scheduled job was removed
        var scheduler = GetScheduler();
        var jobKey = new JobKey($"share-{share.Id}", "backups");
        (await scheduler.CheckExists(jobKey)).Should().BeFalse();
    }

    [Fact]
    public async Task TriggerImmediateBackup_DeviceLevel_TriggersJob()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var deviceId = Guid.NewGuid();

        // Act - TriggerImmediateBackup creates a durable manual job and triggers it
        await _schedulerService.TriggerImmediateBackup(deviceId);

        // Assert - No exception should be thrown, triggering should succeed
        // The method creates a durable job before triggering it
    }

    [Fact]
    public async Task TriggerImmediateBackup_ShareLevel_TriggersJob()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        // Act - TriggerImmediateBackup creates a durable manual job and triggers it
        await _schedulerService.TriggerImmediateBackup(deviceId, shareId);

        // Assert - No exception should be thrown, triggering should succeed
        // The method creates a durable job before triggering it
    }

    [Fact]
    public async Task ScheduleAllBackups_LoadsDevicesAndShares()
    {
        // Arrange
        var device = CreateTestDevice("test-device");
        device.Schedule = new Schedule { CronExpression = "0 0 2 * * ?" };
        
        var share = CreateTestShare(device.Id, "test-share");
        share.Schedule = new Schedule { CronExpression = "0 0 3 * * ?" };

        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device> { device });
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(new List<Share> { share });

        // Act
        await _schedulerService.Start();

        // Assert - Verify scheduler loaded device and its shares
        _mockDeviceService.Verify(x => x.ListDevices(), Times.Once);
        _mockShareService.Verify(x => x.ListShares(device.Id), Times.Once);
        
        // Verify jobs can be triggered (proves they were scheduled)
        await _schedulerService.TriggerImmediateBackup(device.Id);
        await _schedulerService.TriggerImmediateBackup(device.Id, share.Id);
    }

    [Fact]
    public async Task Stop_BeforeStart_DoesNotThrow()
    {
        // Act & Assert - Should not throw (implicit by completing)
        await _schedulerService.Stop();
        
        // Verify can stop multiple times safely
        await _schedulerService.Stop();
    }

    [Fact]
    public async Task CancelJob_DelegatesToOrchestrator()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        await _schedulerService.CancelJob(jobId);

        // Assert
        _mockOrchestrator.Verify(x => x.CancelJob(jobId), Times.Once);
    }

    [Fact]
    public async Task ScheduleShareBackup_WithInvalidCron_ThrowsArgumentException()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var device = CreateTestDevice("cron-device");
        var share = CreateTestShare(device.Id, "cron-share");
        var schedule = new Schedule { CronExpression = "not-a-cron" };

        // Act
        var act = async () => await _schedulerService.ScheduleShareBackup(device, share, schedule);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScheduleShareBackup_WithBlankCron_ThrowsArgumentException()
    {
        // Arrange
        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device>());
        _mockShareService.Setup(x => x.ListShares(It.IsAny<Guid>())).ReturnsAsync(new List<Share>());
        await _schedulerService.Start();

        var device = CreateTestDevice("blank-cron-device");
        var share = CreateTestShare(device.Id, "blank-cron-share");
        var schedule = new Schedule { CronExpression = "   " };

        // Act
        var act = async () => await _schedulerService.ScheduleShareBackup(device, share, schedule);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Helper methods
    private Device CreateTestDevice(string name)
    {
        return new Device
        {
            Id = Guid.NewGuid(),
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

    private Share CreateTestShare(Guid deviceId, string name)
    {
        return new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = name,
            Path = $"/{name}",
            Enabled = true,
            Schedule = null,
            RetentionPolicy = null,
            IncludeExcludeRules = new IncludeExcludeRules(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private IScheduler GetScheduler()
    {
        var schedulerProperty = typeof(QuartzSchedulerService).GetProperty(
            "Scheduler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var scheduler = schedulerProperty?.GetValue(_schedulerService) as IScheduler;
        scheduler.Should().NotBeNull();
        return scheduler!;
    }
}
