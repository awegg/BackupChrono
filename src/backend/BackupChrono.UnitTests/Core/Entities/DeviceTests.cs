using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using Xunit;

namespace BackupChrono.UnitTests.Core.Entities;

public class DeviceTests
{
    [Fact]
    public void Validate_ShouldThrow_WhenWakeOnLanEnabledButMacAddressMissing()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("password123"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = null
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => device.Validate());
        Assert.Contains("WakeOnLanMacAddress is required when WakeOnLanEnabled is true", exception.Message);
        Assert.Contains("test-device", exception.Message);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenWakeOnLanEnabledButMacAddressEmpty()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("password123"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "   "
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => device.Validate());
        Assert.Contains("WakeOnLanMacAddress is required when WakeOnLanEnabled is true", exception.Message);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenWakeOnLanEnabledWithValidMacAddress()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("password123"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "00:11:22:33:44:55"
        };

        // Act & Assert - Should not throw
        device.Validate();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenWakeOnLanDisabled()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("password123"),
            WakeOnLanEnabled = false,
            WakeOnLanMacAddress = null
        };

        // Act & Assert - Should not throw
        device.Validate();
    }
}
