using BackupChrono.Api.DTOs;
using BackupChrono.Api.Services;
using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BackupChrono.UnitTests.Api;

public class MappingServiceTests
{
    private readonly MappingService _service;

    public MappingServiceTests()
    {
        _service = new MappingService();
    }

    #region Device Mapping Tests

    [Fact]
    public void ToDeviceDto_MapsAllProperties()
    {
        // Arrange
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "TestDevice",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.1",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "00:11:22:33:44:55",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var dto = _service.ToDeviceDto(device);

        // Assert
        dto.Id.Should().Be(device.Id);
        dto.Name.Should().Be(device.Name);
        dto.Protocol.Should().Be("SMB");
        dto.Host.Should().Be(device.Host);
        dto.Port.Should().Be(device.Port);
        dto.Username.Should().Be(device.Username);
        dto.WakeOnLanEnabled.Should().BeTrue();
        dto.WakeOnLanMacAddress.Should().Be(device.WakeOnLanMacAddress);
    }

    [Fact]
    public void ToDevice_CreatesDeviceWithCorrectProtocol()
    {
        // Arrange
        var dto = new DeviceCreateDto
        {
            Name = "NewDevice",
            Protocol = "SSH",
            Host = "192.168.1.2",
            Port = 22,
            Username = "root",
            Password = "secret"
        };

        // Act
        var device = _service.ToDevice(dto);

        // Assert
        device.Name.Should().Be(dto.Name);
        device.Protocol.Should().Be(ProtocolType.SSH);
        device.Host.Should().Be(dto.Host);
        device.Port.Should().Be(dto.Port);
        device.Username.Should().Be(dto.Username);
        device.Password.Should().NotBeNull();
        device.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("SMB")]
    [InlineData("SSH")]
    [InlineData("Rsync")]
    [InlineData("smb")] // lowercase
    [InlineData("ssh")] // lowercase
    public void ToDevice_HandlesValidProtocols(string protocol)
    {
        // Arrange
        var dto = new DeviceCreateDto
        {
            Name = "Device",
            Protocol = protocol,
            Host = "localhost",
            Username = "user",
            Password = "pass"
        };

        // Act
        var device = _service.ToDevice(dto);

        // Assert
        device.Protocol.ToString().Should().BeEquivalentTo(protocol);
    }

    [Fact]
    public void ToDevice_ThrowsOnInvalidProtocol()
    {
        // Arrange
        var dto = new DeviceCreateDto
        {
            Name = "Device",
            Protocol = "InvalidProtocol",
            Host = "localhost",
            Username = "user",
            Password = "pass"
        };

        // Act
        Action act = () => _service.ToDevice(dto);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid protocol*");
    }

    [Fact]
    public void ApplyUpdate_UpdatesDeviceProperties()
    {
        // Arrange
        var device = new Device
        {
            Name = "OldName",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.1",
            Username = "olduser",
            Password = new EncryptedCredential("oldpass")
        };

        var updateDto = new DeviceUpdateDto
        {
            Name = "NewName",
            Host = "192.168.1.100",
            Port = 2222,
            Username = "newuser",
            Password = "newpass",
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "AA:BB:CC:DD:EE:FF"
        };

        // Act
        _service.ApplyUpdate(device, updateDto);

        // Assert
        device.Name.Should().Be("NewName");
        device.Host.Should().Be("192.168.1.100");
        device.Port.Should().Be(2222);
        device.Username.Should().Be("newuser");
        device.WakeOnLanEnabled.Should().BeTrue();
        device.WakeOnLanMacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
    }

    #endregion

    #region Schedule Mapping Tests

    [Fact]
    public void ToScheduleDto_MapsScheduleCorrectly()
    {
        // Arrange
        var schedule = new Schedule
        {
            CronExpression = "0 0 2 * * ?",
            TimeWindowStart = new TimeOnly(2, 0),
            TimeWindowEnd = new TimeOnly(6, 0)
        };

        // Act
        var dto = _service.ToScheduleDto(schedule);

        // Assert
        dto.Should().NotBeNull();
        dto!.CronExpression.Should().Be("0 0 2 * * ?");
        dto.TimeWindowStart.Should().Be(new TimeOnly(2, 0));
        dto.TimeWindowEnd.Should().Be(new TimeOnly(6, 0));
    }

    [Fact]
    public void ToScheduleDto_WithNull_ReturnsNull()
    {
        // Act
        var dto = _service.ToScheduleDto(null);

        // Assert
        dto.Should().BeNull();
    }

    [Fact]
    public void ToSchedule_CreatesScheduleCorrectly()
    {
        // Arrange
        var dto = new ScheduleDto
        {
            CronExpression = "0 30 3 * * ?",
            TimeWindowStart = new TimeOnly(3, 0),
            TimeWindowEnd = new TimeOnly(5, 0)
        };

        // Act
        var schedule = _service.ToSchedule(dto);

        // Assert
        schedule.Should().NotBeNull();
        schedule!.CronExpression.Should().Be("0 30 3 * * ?");
        schedule.TimeWindowStart.Should().Be(new TimeOnly(3, 0));
        schedule.TimeWindowEnd.Should().Be(new TimeOnly(5, 0));
    }

    [Fact]
    public void ToSchedule_WithNull_ReturnsNull()
    {
        // Act
        var schedule = _service.ToSchedule(null);

        // Assert
        schedule.Should().BeNull();
    }

    #endregion

    #region RetentionPolicy Mapping Tests

    [Fact]
    public void ToRetentionPolicyDto_MapsAllProperties()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            KeepLatest = 5,
            KeepDaily = 7,
            KeepWeekly = 4,
            KeepMonthly = 12,
            KeepYearly = 3
        };

        // Act
        var dto = _service.ToRetentionPolicyDto(policy);

        // Assert
        dto.Should().NotBeNull();
        dto!.KeepLatest.Should().Be(5);
        dto.KeepDaily.Should().Be(7);
        dto.KeepWeekly.Should().Be(4);
        dto.KeepMonthly.Should().Be(12);
        dto.KeepYearly.Should().Be(3);
    }

    [Fact]
    public void ToRetentionPolicy_CreatesCorrectPolicy()
    {
        // Arrange
        var dto = new RetentionPolicyDto
        {
            KeepLatest = 10,
            KeepDaily = 14,
            KeepWeekly = 8,
            KeepMonthly = 6,
            KeepYearly = 2
        };

        // Act
        var policy = _service.ToRetentionPolicy(dto);

        // Assert
        policy.Should().NotBeNull();
        policy!.KeepLatest.Should().Be(10);
        policy.KeepDaily.Should().Be(14);
        policy.KeepWeekly.Should().Be(8);
        policy.KeepMonthly.Should().Be(6);
        policy.KeepYearly.Should().Be(2);
    }

    #endregion

    #region IncludeExcludeRules Mapping Tests

    [Fact]
    public void ToIncludeExcludeRulesDto_MapsPatterns()
    {
        // Arrange
        var rules = new IncludeExcludeRules
        {
            ExcludePatterns = new[] { "temp/*", "*.tmp" },
            ExcludeRegex = new[] { "^backup_.*" },
            IncludeOnlyRegex = Array.Empty<string>(),
            ExcludeIfPresent = new[] { ".nobackup" }
        };

        // Act
        var dto = _service.ToIncludeExcludeRulesDto(rules);

        // Assert
        dto.Should().NotBeNull();
        dto!.ExcludePatterns.Should().BeEquivalentTo(new[] { "temp/*", "*.tmp" });
        dto.ExcludeRegex.Should().BeEquivalentTo(new[] { "^backup_.*" });
        dto.ExcludeIfPresent.Should().BeEquivalentTo(new[] { ".nobackup" });
    }

    [Fact]
    public void ToIncludeExcludeRules_CreatesRulesCorrectly()
    {
        // Arrange
        var dto = new IncludeExcludeRulesDto
        {
            ExcludePatterns = new[] { "cache/*", "*.cache" },
            IncludeOnlyRegex = new[] { ".*\\.jpg$", ".*\\.png$" }
        };

        // Act
        var rules = _service.ToIncludeExcludeRules(dto);

        // Assert
        rules.Should().NotBeNull();
        rules!.ExcludePatterns.Should().BeEquivalentTo(new[] { "cache/*", "*.cache" });
        rules.IncludeOnlyRegex.Should().BeEquivalentTo(new[] { ".*\\.jpg$", ".*\\.png$" });
    }

    #endregion

    #region Share Mapping Tests

    [Fact]
    public void ToShareDto_MapsAllProperties()
    {
        // Arrange
        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Name = "Documents",
            Path = "/mnt/documents",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var dto = _service.ToShareDto(share);

        // Assert
        dto.Id.Should().Be(share.Id);
        dto.DeviceId.Should().Be(share.DeviceId);
        dto.Name.Should().Be(share.Name);
        dto.Path.Should().Be(share.Path);
        dto.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ToShare_CreatesShareWithDeviceId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var dto = new ShareCreateDto
        {
            Name = "Photos",
            Path = "/photos",
            Enabled = true
        };

        // Act
        var share = _service.ToShare(deviceId, dto);

        // Assert
        share.DeviceId.Should().Be(deviceId);
        share.Name.Should().Be(dto.Name);
        share.Path.Should().Be(dto.Path);
        share.Enabled.Should().BeTrue();
        share.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyUpdate_UpdatesShareProperties()
    {
        // Arrange
        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Name = "OldName",
            Path = "/old/path",
            Enabled = false
        };

        var updateDto = new ShareUpdateDto
        {
            Name = "NewName",
            Path = "/new/path",
            Enabled = true
        };

        // Act
        _service.ApplyUpdate(share, updateDto);

        // Assert
        share.Name.Should().Be("NewName");
        share.Path.Should().Be("/new/path");
        share.Enabled.Should().BeTrue();
    }

    #endregion

    #region Backup Mapping Tests

    [Fact]
    public void ToBackupDto_MapsAllProperties()
    {
        // Arrange
        var backup = new Backup
        {
            Id = "backup123",
            DeviceId = Guid.NewGuid(),
            DeviceName = "TestDevice",
            Timestamp = DateTime.UtcNow,
            Status = BackupStatus.Success,
            FilesNew = 10,
            FilesChanged = 5,
            FilesUnmodified = 100,
            DataAdded = 1024 * 1024,
            DataProcessed = 5 * 1024 * 1024,
            Duration = TimeSpan.FromMinutes(5)
        };

        // Act
        var dto = _service.ToBackupDto(backup);

        // Assert
        dto.Id.Should().Be(backup.Id);
        dto.DeviceId.Should().Be(backup.DeviceId);
        dto.DeviceName.Should().Be(backup.DeviceName);
        dto.Status.Should().Be("Success");
        dto.FileStats.New.Should().Be(10);
        dto.FileStats.Changed.Should().Be(5);
        dto.DataStats.Added.Should().Be(1024 * 1024);
    }

    [Theory]
    [InlineData(BackupStatus.Success, "Success")]
    [InlineData(BackupStatus.Failed, "Failed")]
    [InlineData(BackupStatus.Partial, "Partial")]
    public void ToBackupDto_MapsStatusCorrectly(BackupStatus status, string expectedString)
    {
        // Arrange
        var backup = new Backup
        {
            Id = "test",
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device",
            Timestamp = DateTime.UtcNow,
            Status = status,
            FilesNew = 0,
            FilesChanged = 0,
            FilesUnmodified = 0,
            DataAdded = 0,
            DataProcessed = 0,
            Duration = TimeSpan.Zero
        };

        // Act
        var dto = _service.ToBackupDto(backup);

        // Assert
        dto.Status.Should().Be(expectedString);
    }

    #endregion
}
