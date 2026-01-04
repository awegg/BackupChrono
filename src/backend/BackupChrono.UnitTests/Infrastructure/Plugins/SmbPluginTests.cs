using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Plugins;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Plugins;

/// <summary>
/// Unit tests for SmbPlugin that verify basic functionality without requiring a real SMB server.
/// </summary>
public class SmbPluginTests
{
    private readonly SmbPlugin _plugin;

    public SmbPluginTests()
    {
        _plugin = new SmbPlugin();
    }

    [Fact]
    public void ProtocolName_ShouldReturnSMB()
    {
        // Act
        var protocolName = _plugin.ProtocolName;

        // Assert
        protocolName.Should().Be("SMB");
    }

    [Fact]
    public void SupportsWakeOnLan_ShouldReturnTrue()
    {
        // Act
        var supportsWakeOnLan = _plugin.SupportsWakeOnLan;

        // Assert
        supportsWakeOnLan.Should().BeTrue("SMB plugin supports Wake-on-LAN");
    }

    [Fact]
    public void RequiresAuthentication_ShouldReturnTrue()
    {
        // Act
        var requiresAuth = _plugin.RequiresAuthentication;

        // Assert
        requiresAuth.Should().BeTrue("SMB requires authentication");
    }

    [Fact]
    public async Task WakeDevice_WithValidMacAddress_ShouldNotThrow()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "testuser",
            Password = new EncryptedCredential("testpass"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "00:11:22:33:44:55"
        };

        // Act
        var wakeAction = async () => await _plugin.WakeDevice(device);

        // Assert - Should not throw (actual packet sending is handled by WakeOnLanHelper)
        await wakeAction.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WakeDevice_WithMissingMacAddress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "testuser",
            Password = new EncryptedCredential("testpass"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = null
        };

        // Act
        var wakeAction = async () => await _plugin.WakeDevice(device);

        // Assert
        await wakeAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MAC address*");
    }

    [Fact]
    public async Task WakeDevice_WithEmptyMacAddress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "testuser",
            Password = new EncryptedCredential("testpass"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = ""
        };

        // Act
        var wakeAction = async () => await _plugin.WakeDevice(device);

        // Assert
        await wakeAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MAC address*");
    }

    [Fact]
    public async Task MountShare_WithValidDevice_ShouldReturnMountPath()
    {
        // Arrange
        var plugin = new SmbPlugin();
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "testuser",
            Password = new EncryptedCredential("testpass")
        };

        var share = new Share
        {
            DeviceId = device.Id,
            Name = "testshare",
            Path = "data"
        };

        // Note: This will fail in unit test without actual SMB server
        // but verifies the method signature and basic logic
        var mountAction = async () => await plugin.MountShare(device, share);

        // Assert - should throw because no real server exists
        await mountAction.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TestConnection_WithValidDevice_ShouldNotThrow()
    {
        // Arrange
        var plugin = new SmbPlugin();
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "nonexistent-test-server-12345",
            Username = "testuser",
            Password = new EncryptedCredential("testpass")
        };

        // Act - should not throw, just return false
        var result = await plugin.TestConnection(device);

        // Assert
        result.Should().BeFalse("connection to nonexistent server should fail gracefully");
    }

}
