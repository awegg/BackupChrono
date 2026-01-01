using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using FluentAssertions;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BackupChrono.IntegrationTests.Controllers;

public class DevicesControllerTests : IAsyncLifetime
{
    private BackupChronoWebApplicationFactory _factory = null!;
    private HttpClient _httpClient = null!;

    public async Task InitializeAsync()
    {
        _factory = new BackupChronoWebApplicationFactory();
        _httpClient = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ListDevices_ReturnsEmptyList_WhenNoDevices()
    {
        // Arrange
        _factory.MockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device>());

        // Act
        var response = await _httpClient.GetAsync("api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var devices = JsonDocument.Parse(content).RootElement.EnumerateArray().ToList();
        devices.Should().BeEmpty();
    }

    [Fact]
    public async Task ListDevices_ReturnsDeviceList_WhenDevicesExist()
    {
        // Arrange
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = "test-nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.ListDevices())
            .ReturnsAsync(new List<Device> { device });

        // Act
        var response = await _httpClient.GetAsync("api/devices");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var devices = json.RootElement.EnumerateArray().ToList();

        devices.Should().HaveCount(1);
        devices[0].GetProperty("name").GetString().Should().Be("test-nas");
        devices[0].GetProperty("protocol").GetString().Should().Be("SMB");
        devices[0].GetProperty("host").GetString().Should().Be("192.168.1.100");
    }

    [Fact]
    public async Task GetDevice_Returns404_WhenDeviceNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync((Device?)null);

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Device not found");
    }

    [Fact]
    public async Task GetDevice_ReturnsDeviceDetail_WhenDeviceExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "test-nas",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(device);

        _factory.MockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share>());

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.GetProperty("id").GetString().Should().Be(deviceId.ToString());
        root.GetProperty("name").GetString().Should().Be("test-nas");
        root.GetProperty("shares").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CreateDevice_ReturnsCreated_WithValidData()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var createDto = new DeviceCreateDto
        {
            Name = "new-device",
            Protocol = "SMB",
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = "password"
        };

        var createdDevice = new Device
        {
            Id = deviceId,
            Name = createDto.Name,
            Protocol = ProtocolType.SMB,
            Host = createDto.Host,
            Port = createDto.Port,
            Username = createDto.Username,
            Password = new EncryptedCredential(createDto.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.CreateDevice(It.IsAny<Device>()))
            .ReturnsAsync(createdDevice);

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync("api/devices", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseJson = JsonDocument.Parse(responseContent);
        
        responseJson.RootElement.GetProperty("name").GetString().Should().Be("new-device");
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateDevice_ReturnsBadRequest_WithInvalidProtocol()
    {
        // Arrange
        var createDto = new DeviceCreateDto
        {
            Name = "test-device",
            Protocol = "INVALID_PROTOCOL",
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = "password"
        };

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync("api/devices", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Invalid protocol");
    }

    [Fact]
    public async Task UpdateDevice_ReturnsOk_WithValidUpdate()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var updateDto = new DeviceUpdateDto
        {
            Name = "updated-device"
        };

        var updatedDevice = new Device
        {
            Id = deviceId,
            Name = "updated-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(updatedDevice);

        _factory.MockDeviceService
            .Setup(x => x.UpdateDevice(It.IsAny<Device>()))
            .ReturnsAsync(updatedDevice);

        var json = JsonSerializer.Serialize(updateDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PutAsync($"api/devices/{deviceId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteDevice_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "device-to-delete",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(device);

        _factory.MockDeviceService
            .Setup(x => x.DeleteDevice(deviceId))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _httpClient.DeleteAsync($"api/devices/{deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TestConnection_ReturnsOk_WhenConnectionSucceeds()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(device);

        _factory.MockDeviceService
            .Setup(x => x.TestConnection(deviceId))
            .ReturnsAsync(true);

        // Act
        var response = await _httpClient.PostAsync($"api/devices/{deviceId}/test-connection", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("true");
    }

    [Fact]
    public async Task TestConnection_Returns500_WhenConnectionFails()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(device);

        _factory.MockDeviceService
            .Setup(x => x.TestConnection(deviceId))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        // Act
        var response = await _httpClient.PostAsync($"api/devices/{deviceId}/test-connection", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Failed to test connection");
    }
}
