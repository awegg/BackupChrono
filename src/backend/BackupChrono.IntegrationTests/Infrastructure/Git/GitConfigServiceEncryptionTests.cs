using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Git;
using Xunit;

namespace BackupChrono.IntegrationTests.Infrastructure.Git;

public class GitConfigServiceEncryptionTests : IDisposable
{
    private readonly string _tempPath;
    private readonly GitConfigService _service;

    public GitConfigServiceEncryptionTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"backupchrono-test-{Guid.NewGuid()}");
        _service = new GitConfigService(_tempPath);
        _service.InitializeRepository();
    }

    [Fact]
    public async Task SaveDevice_EncryptsPasswordInYaml()
    {
        // Arrange
        var device = new Device
        {
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "admin",
            Password = new EncryptedCredential("myPlaintextPassword123!")
        };
        
        // Act
        await _service.SaveDevice(device);
        
        // Assert
        var yamlPath = Path.Combine(_tempPath, "devices", "test-device.yaml");
        var yamlContent = await File.ReadAllTextAsync(yamlPath);
        
        // Password should NOT appear in plaintext
        Assert.DoesNotContain("myPlaintextPassword123!", yamlContent);
        
        // Should contain some encrypted value (base64 string)
        Assert.Contains("password:", yamlContent);
        
        // Verify the encrypted value is present and not empty
        Assert.Matches(@"password:\s+\S+", yamlContent);
    }

    [Fact]
    public async Task LoadDevice_DecryptsPasswordCorrectly()
    {
        // Arrange
        const string originalPassword = "secretPassword456@";
        var device = new Device
        {
            Name = "test-device-2",
            Protocol = ProtocolType.SSH,
            Host = "server.example.com",
            Username = "backup",
            Password = new EncryptedCredential(originalPassword)
        };
        
        await _service.SaveDevice(device);
        
        // Act
        var loadedDevice = await _service.LoadDevice("test-device-2");
        
        // Assert
        Assert.NotNull(loadedDevice);
        Assert.Equal(originalPassword, loadedDevice.Password.GetPlaintext());
    }

    [Fact]
    public async Task RoundTrip_PreservesAllDeviceProperties()
    {
        // Arrange
        var original = new Device
        {
            Name = "nas-server",
            Protocol = ProtocolType.SMB,
            Host = "nas.local",
            Port = 445,
            Username = "backupuser",
            Password = new EncryptedCredential("complex!P@ssw0rd#123"),
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "00:11:22:33:44:55",
            Schedule = new Schedule
            {
                CronExpression = "0 2 * * *"
            },
            RetentionPolicy = new RetentionPolicy
            {
                KeepDaily = 7,
                KeepWeekly = 4,
                KeepMonthly = 6
            }
        };
        
        // Act
        await _service.SaveDevice(original);
        var loaded = await _service.LoadDevice("nas-server");
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.Protocol, loaded.Protocol);
        Assert.Equal(original.Host, loaded.Host);
        Assert.Equal(original.Port, loaded.Port);
        Assert.Equal(original.Username, loaded.Username);
        Assert.Equal(original.Password.GetPlaintext(), loaded.Password.GetPlaintext());
        Assert.Equal(original.WakeOnLanEnabled, loaded.WakeOnLanEnabled);
        Assert.Equal(original.WakeOnLanMacAddress, loaded.WakeOnLanMacAddress);
        Assert.NotNull(loaded.Schedule);
        Assert.Equal(original.Schedule.CronExpression, loaded.Schedule.CronExpression);
    }

    [Fact]
    public async Task MultipleDevices_EachHasUniqueEncryption()
    {
        // Arrange
        var device1 = new Device
        {
            Name = "device1",
            Protocol = ProtocolType.SSH,
            Host = "host1.local",
            Username = "user1",
            Password = new EncryptedCredential("samePassword")
        };
        
        var device2 = new Device
        {
            Name = "device2",
            Protocol = ProtocolType.SSH,
            Host = "host2.local",
            Username = "user2",
            Password = new EncryptedCredential("samePassword")
        };
        
        // Act
        await _service.SaveDevice(device1);
        await _service.SaveDevice(device2);
        
        // Assert
        var yaml1 = await File.ReadAllTextAsync(Path.Combine(_tempPath, "devices", "device1.yaml"));
        var yaml2 = await File.ReadAllTextAsync(Path.Combine(_tempPath, "devices", "device2.yaml"));
        
        // Extract password lines
        var password1Line = yaml1.Split('\n').First(l => l.Contains("password:"));
        var password2Line = yaml2.Split('\n').First(l => l.Contains("password:"));
        
        // Even with same plaintext, encrypted values should differ (random nonce)
        Assert.NotEqual(password1Line, password2Line);
        
        // But both should decrypt to the same value
        var loaded1 = await _service.LoadDevice("device1");
        var loaded2 = await _service.LoadDevice("device2");
        
        Assert.Equal("samePassword", loaded1!.Password.GetPlaintext());
        Assert.Equal("samePassword", loaded2!.Password.GetPlaintext());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempPath))
            {
                // Remove read-only attributes from .git folder
                var dirInfo = new DirectoryInfo(_tempPath);
                SetAttributesNormal(dirInfo);
                Directory.Delete(_tempPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var subDir in dir.GetDirectories())
        {
            SetAttributesNormal(subDir);
        }
        
        foreach (var file in dir.GetFiles())
        {
            file.Attributes = FileAttributes.Normal;
        }
    }
}
