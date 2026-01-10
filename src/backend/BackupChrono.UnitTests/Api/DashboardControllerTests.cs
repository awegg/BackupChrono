using BackupChrono.Api.Controllers;
using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Api;

public class DashboardControllerTests
{
    private readonly Mock<IDeviceService> _deviceServiceMock = new();
    private readonly Mock<IShareService> _shareServiceMock = new();
    private readonly Mock<IBackupJobRepository> _jobRepositoryMock = new();
    private readonly Mock<IStorageMonitor> _storageMonitorMock = new();
    private readonly Mock<ILogger<DashboardController>> _loggerMock = new();
    private readonly IOptions<ResticOptions> _resticOptions = Options.Create(new ResticOptions
    {
        RepositoryBasePath = "/repo"
    });

    [Fact]
    public async Task GetSummary_ComputesStatsAndStatuses()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareOneId = Guid.NewGuid();
        var shareTwoId = Guid.NewGuid();
        var lastSuccessful = DateTime.UtcNow.AddHours(-4);

        var device = new Device
        {
            Id = deviceId,
            Name = "nas-01",
            Protocol = ProtocolType.SMB,
            Host = "nas.local",
            Username = "user",
            Password = new EncryptedCredential("secret"),
            Schedule = new Schedule { CronExpression = "0 0 6 * * ?" }
        };

        var shareOne = new Share
        {
            Id = shareOneId,
            DeviceId = deviceId,
            Name = "docs",
            Path = "/docs",
            Enabled = true,
            Schedule = new Schedule { CronExpression = "0 15 6 * * ?" }
        };

        var shareTwo = new Share
        {
            Id = shareTwoId,
            DeviceId = deviceId,
            Name = "media",
            Path = "/media",
            Enabled = false,
            Schedule = new Schedule { CronExpression = "not-a-cron" }
        };

        var jobs = new List<BackupJob>
        {
            new()
            {
                DeviceId = deviceId,
                ShareId = shareOneId,
                Status = BackupJobStatus.Running,
                Type = BackupJobType.Scheduled,
                StartedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new()
            {
                DeviceId = deviceId,
                ShareId = shareOneId,
                Status = BackupJobStatus.Failed,
                Type = BackupJobType.Scheduled,
                StartedAt = DateTime.UtcNow.AddHours(-3),
                CompletedAt = DateTime.UtcNow.AddHours(-2)
            },
            new()
            {
                DeviceId = deviceId,
                ShareId = shareOneId,
                Status = BackupJobStatus.Completed,
                Type = BackupJobType.Scheduled,
                StartedAt = DateTime.UtcNow.AddHours(-5),
                CompletedAt = lastSuccessful,
                BackupId = "snap-123",
                FilesProcessed = 42,
                BytesTransferred = 1234
            },
            new()
            {
                DeviceId = deviceId,
                ShareId = shareTwoId,
                Status = BackupJobStatus.Completed,
                Type = BackupJobType.Scheduled,
                StartedAt = DateTime.UtcNow.AddHours(-7),
                CompletedAt = DateTime.UtcNow.AddHours(-6)
            }
        };

        _deviceServiceMock.Setup(s => s.ListDevices()).ReturnsAsync(new List<Device> { device });
        _shareServiceMock.Setup(s => s.ListShares(deviceId)).ReturnsAsync(new List<Share> { shareOne, shareTwo });
        _jobRepositoryMock.Setup(r => r.ListJobs()).ReturnsAsync(jobs);
        _storageMonitorMock.Setup(m => m.GetStorageStatus("/repo"))
            .ReturnsAsync(new StorageStatus
            {
                Path = "/repo",
                UsedBytes = 4096,
                ThresholdLevel = StorageThresholdLevel.Normal
            });

        var controller = CreateController();

        // Act
        var result = await controller.GetSummary();

        // Assert
        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        var summary = ok!.Value as DashboardSummaryDto;
        summary.Should().NotBeNull();

        summary!.Stats.TotalDevices.Should().Be(1);
        summary.Stats.TotalShares.Should().Be(2);
        summary.Stats.TotalStoredBytes.Should().Be(4096);
        summary.Stats.RecentFailures.Should().Be(1);
        summary.Stats.RunningJobs.Should().Be(1);
        summary.Stats.SystemHealth.Should().Be("Warning");

        var deviceResult = summary.Devices.Single();
        deviceResult.Name.Should().Be("nas-01");
        deviceResult.Shares.Should().HaveCount(2);

        var runningShare = deviceResult.Shares.Single(s => s.Id == shareOneId);
        runningShare.Status.Should().Be("Running");
        runningShare.LastBackupTime.Should().Be(lastSuccessful);
        runningShare.LastBackupId.Should().Be("snap-123");
        runningShare.NextBackupTime.Should().NotBeNull();

        var disabledShare = deviceResult.Shares.Single(s => s.Id == shareTwoId);
        disabledShare.Status.Should().Be("Disabled");
        disabledShare.NextBackupTime.Should().BeNull();
    }

    [Fact]
    public async Task GetSummary_HandlesStorageAndCronFailuresGracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        var device = new Device
        {
            Id = deviceId,
            Name = "nas-02",
            Protocol = ProtocolType.SMB,
            Host = "nas.local",
            Username = "user",
            Password = new EncryptedCredential("secret"),
            Schedule = null
        };

        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "data",
            Path = "/data",
            Enabled = true,
            Schedule = new Schedule { CronExpression = "invalid" }
        };

        _deviceServiceMock.Setup(s => s.ListDevices()).ReturnsAsync(new List<Device> { device });
        _shareServiceMock.Setup(s => s.ListShares(deviceId)).ReturnsAsync(new List<Share> { share });
        _jobRepositoryMock.Setup(r => r.ListJobs()).ReturnsAsync(new List<BackupJob>());
        _storageMonitorMock.Setup(m => m.GetStorageStatus("/repo"))
            .ThrowsAsync(new InvalidOperationException("storage offline"));

        var controller = CreateController();

        // Act
        var result = await controller.GetSummary();

        // Assert
        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull();
        var summary = ok!.Value as DashboardSummaryDto;
        summary.Should().NotBeNull();

        summary!.Stats.TotalDevices.Should().Be(1);
        summary.Stats.TotalShares.Should().Be(1);
        summary.Stats.TotalStoredBytes.Should().Be(0);
        summary.Stats.SystemHealth.Should().Be("Healthy");
        summary.Stats.RunningJobs.Should().Be(0);

        var shareDto = summary.Devices.Single().Shares.Single();
        shareDto.Status.Should().Be("Pending");
        shareDto.NextBackupTime.Should().BeNull();
    }

    private DashboardController CreateController()
    {
        return new DashboardController(
            _deviceServiceMock.Object,
            _shareServiceMock.Object,
            _jobRepositoryMock.Object,
            _storageMonitorMock.Object,
            _resticOptions,
            _loggerMock.Object);
    }
}
