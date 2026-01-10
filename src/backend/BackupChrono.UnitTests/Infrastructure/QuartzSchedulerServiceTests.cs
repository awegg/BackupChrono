using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure;

public class QuartzSchedulerServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<ILogger<QuartzSchedulerService>> _loggerMock;
    private readonly Mock<IDeviceService> _deviceServiceMock;
    private readonly Mock<IShareService> _shareServiceMock;

    public QuartzSchedulerServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _loggerMock = new Mock<ILogger<QuartzSchedulerService>>();
        _deviceServiceMock = new Mock<IDeviceService>();
        _shareServiceMock = new Mock<IShareService>();

        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IDeviceService)))
            .Returns(_deviceServiceMock.Object);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IShareService)))
            .Returns(_shareServiceMock.Object);
        
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
    }

    [Theory]
    [InlineData("0 0 0 * * *", "0 0 0 * * ?")] // Both wildcards → normalize
    [InlineData("0 0 12 * * *", "0 0 12 * * ?")] // Both wildcards → normalize
    [InlineData("0 30 14 * * MON", "0 30 14 * * MON")] // Specific day → no change
    [InlineData("0 0 0 1 * ?", "0 0 0 1 * ?")] // Already has ? → no change
    [InlineData("0 0 0 ? * MON", "0 0 0 ? * MON")] // Already has ? → no change
    [InlineData("0 0 0 1-15 * ?", "0 0 0 1-15 * ?")] // Range → no change
    [InlineData("", "")] // Empty → no change
    public void NormalizeCronExpression_HandlesVariousFormats(string input, string expected)
    {
        // Arrange
        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        // Act - use reflection to access private method
        var method = typeof(QuartzSchedulerService).GetMethod(
            "NormalizeCronExpression",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(service, new object[] { input }) as string;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeCronExpression_HandlesNullOrWhitespace()
    {
        // Arrange
        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        var method = typeof(QuartzSchedulerService).GetMethod(
            "NormalizeCronExpression",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert - null input
        var resultNull = method?.Invoke(service, new object?[] { null }) as string;
        resultNull.Should().BeNull();

        // Act & Assert - whitespace input
        var resultWhitespace = method?.Invoke(service, new object[] { "   " }) as string;
        resultWhitespace.Should().Be("   ");
    }

    [Fact]
    public void NormalizeCronExpression_HandlesInvalidFormat()
    {
        // Arrange
        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        var method = typeof(QuartzSchedulerService).GetMethod(
            "NormalizeCronExpression",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act - too few parts (less than 6)
        var result = method?.Invoke(service, new object[] { "0 0 0" }) as string;

        // Assert - returns unchanged
        result.Should().Be("0 0 0");
    }

    [Fact]
    public async Task Start_InitializesScheduler()
    {
        // Arrange
        _deviceServiceMock.Setup(s => s.ListDevices()).ReturnsAsync(new List<Device>());
        
        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        try
        {
            // Act
            await service.Start();

            // Assert - verify scheduler property is not null using reflection
            var schedulerProperty = typeof(QuartzSchedulerService).GetProperty(
                "Scheduler",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var scheduler = schedulerProperty?.GetValue(service);
            scheduler.Should().NotBeNull();
        }
        finally
        {
            await service.Stop();
        }
    }

    [Fact]
    public async Task Stop_WithUninitializedScheduler_DoesNotThrow()
    {
        // Arrange
        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        // Act
        Func<Task> act = async () => await service.Stop();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ScheduleAllBackups_WithNoDevices_CompletesSuccessfully()
    {
        // Arrange
        _deviceServiceMock.Setup(s => s.ListDevices()).ReturnsAsync(new List<Device>());
        
        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        try
        {
            await service.Start();

            // Act
            Func<Task> act = async () => await service.ScheduleAllBackups();

            // Assert
            await act.Should().NotThrowAsync();
        }
        finally
        {
            await service.Stop();
        }
    }

    [Fact]
    public async Task ScheduleAllBackups_NormalizesCronExpressions()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "TestDevice",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.1",
            Username = "user",
            Password = new EncryptedCredential("password"),
            Schedule = new Schedule { CronExpression = "0 0 0 * * *" } // Both wildcards
        };

        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = "TestShare",
            Path = "/data",
            Enabled = true
        };

        _deviceServiceMock.Setup(s => s.ListDevices()).ReturnsAsync(new List<Device> { device });
        _shareServiceMock.Setup(s => s.ListShares(deviceId)).ReturnsAsync(new List<Share> { share });

        var service = new QuartzSchedulerService(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            $"TestScheduler_{Guid.NewGuid()}");

        try
        {
            await service.Start();

            // Act
            await service.ScheduleAllBackups();

            // Assert - verify normalization warning was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Normalized cron expression")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            await service.Stop();
        }
    }
}
