using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure;

public class RetentionPolicyServiceTests : IDisposable
{
    private readonly Mock<IResticService> _mockResticService;
    private readonly Mock<IGlobalConfigRepository> _mockGlobalConfigRepo;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IDeviceService> _mockDeviceService;
    private readonly Mock<IShareService> _mockShareService;
    private readonly IConfiguration _configuration;
    private readonly RetentionPolicyService _service;
    private readonly string _testLogDirectory;

    public RetentionPolicyServiceTests()
    {
        _mockResticService = new Mock<IResticService>();
        _mockGlobalConfigRepo = new Mock<IGlobalConfigRepository>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockDeviceService = new Mock<IDeviceService>();
        _mockShareService = new Mock<IShareService>();
        
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IDeviceService))).Returns(_mockDeviceService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IShareService))).Returns(_mockShareService.Object);
        
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"retention-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDirectory);
        
        var config = new Dictionary<string, string>
        {
            { "RetentionPolicy:LogDirectory", _testLogDirectory },
            { "ConfigRepository:Path", _testLogDirectory }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config!)
            .Build();
        
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole());
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<RetentionPolicyService>>();
        
        _service = new RetentionPolicyService(
            _mockResticService.Object,
            _mockScopeFactory.Object,
            _mockGlobalConfigRepo.Object,
            _configuration,
            logger
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLogDirectory))
        {
            try { Directory.Delete(_testLogDirectory, true); } catch { }
        }
    }

    [Fact]
    public async Task ApplyForDevice_WithSharePolicy_UsesSharePolicy()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 7 }
        };

        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            Name = "photos",
            Path = "/photos",
            Enabled = true,
            RetentionPolicy = new RetentionPolicy { KeepLatest = 10, KeepWeekly = 8 }
        };

        _mockDeviceService.Setup(x => x.GetDeviceByName("nas")).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(new List<Share> { share });
        _mockResticService.Setup(x => x.ApplyRetentionPolicy("nas", "photos", It.IsAny<RetentionPolicy>(), false, null))
            .ReturnsAsync((5, 1024L * 1024 * 100));

        var results = await _service.ApplyForDevice("nas", false);

        results.Should().HaveCount(1);
        _mockResticService.Verify(x => x.ApplyRetentionPolicy(
            "nas", "photos",
            It.Is<RetentionPolicy>(p => p.KeepLatest == 10 && p.KeepWeekly == 8),
            false, null
        ), Times.Once);
    }

    [Fact]
    public async Task ApplyForDevice_WithDevicePolicy_UsesDevicePolicy()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 7, KeepDaily = 30 }
        };

        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            Name = "docs",
            Path = "/docs",
            Enabled = true
        };

        _mockDeviceService.Setup(x => x.GetDeviceByName("nas")).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(new List<Share> { share });
        _mockResticService.Setup(x => x.ApplyRetentionPolicy("nas", "docs", It.IsAny<RetentionPolicy>(), false, null))
            .ReturnsAsync((3, 1024L * 1024 * 50));

        await _service.ApplyForDevice("nas", false);

        _mockResticService.Verify(x => x.ApplyRetentionPolicy(
            "nas", "docs",
            It.Is<RetentionPolicy>(p => p.KeepLatest == 7 && p.KeepDaily == 30),
            false, null
        ), Times.Once);
    }

    [Fact]
    public async Task ApplyForDevice_WithGlobalPolicy_UsesGlobalPolicy()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass")
        };

        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            Name = "backup",
            Path = "/backup",
            Enabled = true
        };

        var globalPolicy = new RetentionPolicy { KeepLatest = 5, KeepDaily = 14, KeepWeekly = 4 };

        _mockDeviceService.Setup(x => x.GetDeviceByName("nas")).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(new List<Share> { share });
        _mockGlobalConfigRepo.Setup(x => x.GetDefaultRetentionPolicy()).ReturnsAsync(globalPolicy);
        _mockResticService.Setup(x => x.ApplyRetentionPolicy("nas", "backup", It.IsAny<RetentionPolicy>(), false, null))
            .ReturnsAsync((2, 1024L * 1024 * 25));

        await _service.ApplyForDevice("nas", false);

        _mockResticService.Verify(x => x.ApplyRetentionPolicy(
            "nas", "backup",
            It.Is<RetentionPolicy>(p => p.KeepLatest == 5 && p.KeepDaily == 14 && p.KeepWeekly == 4),
            false, null
        ), Times.Once);
    }

    [Fact]
    public async Task ApplyForDevice_SkipsDisabledShares()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 7 }
        };

        var shares = new List<Share>
        {
            new() { Id = Guid.NewGuid(), DeviceId = device.Id, Name = "enabled", Path = "/enabled", Enabled = true },
            new() { Id = Guid.NewGuid(), DeviceId = device.Id, Name = "disabled", Path = "/disabled", Enabled = false }
        };

        _mockDeviceService.Setup(x => x.GetDeviceByName("nas")).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(shares);
        _mockResticService.Setup(x => x.ApplyRetentionPolicy("nas", It.IsAny<string>(), It.IsAny<RetentionPolicy>(), false, null))
            .ReturnsAsync((1, 1024L * 1024));

        var results = await _service.ApplyForDevice("nas", false);

        results.Should().HaveCount(1);
        results.First().ShareName.Should().Be("enabled");
        _mockResticService.Verify(x => x.ApplyRetentionPolicy("nas", "disabled", It.IsAny<RetentionPolicy>(), false, null), Times.Never);
    }

    [Fact]
    public async Task ApplyForDevice_WithError_ReturnsErrorResult()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 7 }
        };

        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            Name = "test",
            Path = "/test",
            Enabled = true
        };

        _mockDeviceService.Setup(x => x.GetDeviceByName("nas")).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(new List<Share> { share });
        _mockResticService.Setup(x => x.ApplyRetentionPolicy("nas", "test", It.IsAny<RetentionPolicy>(), false, null))
            .ThrowsAsync(new InvalidOperationException("Restic failed"));

        var results = await _service.ApplyForDevice("nas", false);

        results.Should().HaveCount(1);
        var result = results.First();
        result.Error.Should().Contain("Restic failed");
        result.SnapshotsPruned.Should().Be(0);
    }

    [Fact]
    public async Task ApplyForAll_ProcessesAllDevices()
    {
        var device1 = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas1",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 7 }
        };

        var device2 = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas2",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.101",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 10 }
        };

        var share1 = new Share { Id = Guid.NewGuid(), DeviceId = device1.Id, Name = "share1", Path = "/share1", Enabled = true };
        var share2 = new Share { Id = Guid.NewGuid(), DeviceId = device2.Id, Name = "share2", Path = "/share2", Enabled = true };

        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device> { device1, device2 });
        _mockDeviceService.Setup(x => x.GetDeviceByName("nas1")).ReturnsAsync(device1);
        _mockDeviceService.Setup(x => x.GetDeviceByName("nas2")).ReturnsAsync(device2);
        _mockShareService.Setup(x => x.ListShares(device1.Id)).ReturnsAsync(new List<Share> { share1 });
        _mockShareService.Setup(x => x.ListShares(device2.Id)).ReturnsAsync(new List<Share> { share2 });
        _mockResticService.Setup(x => x.ApplyRetentionPolicy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RetentionPolicy>(), false, null))
            .ReturnsAsync((3, 1024L * 1024 * 50));

        var results = await _service.ApplyForAll(false);

        results.Should().HaveCount(2);
        results.Select(r => r.DeviceName).Should().Contain(new[] { "nas1", "nas2" });
    }

    [Fact]
    public async Task GetLastRun_ReturnsNullInitially()
    {
        var lastRun = await _service.GetLastRun();
        lastRun.Should().BeNull();
    }

    [Fact]
    public async Task ApplyForAll_UpdatesLastRunSummary()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("pass"),
            RetentionPolicy = new RetentionPolicy { KeepLatest = 7 }
        };

        var share = new Share { Id = Guid.NewGuid(), DeviceId = device.Id, Name = "share", Path = "/share", Enabled = true };

        _mockDeviceService.Setup(x => x.ListDevices()).ReturnsAsync(new List<Device> { device });
        _mockDeviceService.Setup(x => x.GetDeviceByName("nas")).ReturnsAsync(device);
        _mockShareService.Setup(x => x.ListShares(device.Id)).ReturnsAsync(new List<Share> { share });
        _mockResticService.Setup(x => x.ApplyRetentionPolicy("nas", "share", It.IsAny<RetentionPolicy>(), false, null))
            .ReturnsAsync((5, 1024L * 1024 * 100));

        await _service.ApplyForAll(false);
        var lastRun = await _service.GetLastRun();

        lastRun.Should().NotBeNull();
        lastRun!.TotalSnapshotsPruned.Should().Be(5);
        lastRun.TotalSpaceReclaimedBytes.Should().Be(1024L * 1024 * 100);
    }
}
