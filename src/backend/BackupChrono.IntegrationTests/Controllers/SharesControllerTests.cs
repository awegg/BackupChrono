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

public class SharesControllerTests : IAsyncLifetime
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
    public async Task ListShares_Returns404_WhenDeviceNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync((Device?)null);

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}/shares");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Device not found");
    }

    [Fact]
    public async Task ListShares_ReturnsEmptyList_WhenNoShares()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "user",
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
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}/shares");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        var shares = json.RootElement.EnumerateArray().ToList();
        shares.Should().BeEmpty();
    }

    [Fact]
    public async Task ListShares_ReturnsShareList_WhenSharesExist()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "user",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var share = new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/documents",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(device);

        _factory.MockShareService
            .Setup(x => x.ListShares(deviceId))
            .ReturnsAsync(new List<Share> { share });

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}/shares");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var shares = json.RootElement.EnumerateArray().ToList();

        shares.Should().HaveCount(1);
        shares[0].GetProperty("name").GetString().Should().Be("Documents");
        shares[0].GetProperty("path").GetString().Should().Be("/documents");
        shares[0].GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetShare_Returns404_WhenShareNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        _factory.MockShareService
            .Setup(x => x.GetShare(shareId))
            .ReturnsAsync((Share?)null);

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}/shares/{shareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Share not found");
    }

    [Fact]
    public async Task GetShare_ReturnsShareDetail_WhenShareExists()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/documents",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockShareService
            .Setup(x => x.GetShare(shareId))
            .ReturnsAsync(share);

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}/shares/{shareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.GetProperty("id").GetString().Should().Be(shareId.ToString());
        root.GetProperty("name").GetString().Should().Be("Documents");
        root.GetProperty("path").GetString().Should().Be("/documents");
    }

    [Fact]
    public async Task GetShare_Returns404_WhenShareBelongsToDifferentDevice()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var differentDeviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var share = new Share
        {
            Id = shareId,
            DeviceId = differentDeviceId,  // Different device
            Name = "Documents",
            Path = "/documents",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockShareService
            .Setup(x => x.GetShare(shareId))
            .ReturnsAsync(share);

        // Act
        var response = await _httpClient.GetAsync($"api/devices/{deviceId}/shares/{shareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateShare_ReturnsCreated_WithValidData()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            Name = "test-device",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Username = "user",
            Password = new EncryptedCredential("password"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createDto = new ShareCreateDto
        {
            Name = "NewShare",
            Path = "/new-share",
            Enabled = true
        };

        var createdShare = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = createDto.Name,
            Path = createDto.Path,
            Enabled = createDto.Enabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync(device);

        _factory.MockShareService
            .Setup(x => x.CreateShare(It.IsAny<Share>()))
            .ReturnsAsync(createdShare);

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/devices/{deviceId}/shares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateShare_Returns404_WhenDeviceNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var createDto = new ShareCreateDto
        {
            Name = "NewShare",
            Path = "/new-share",
            Enabled = true
        };

        _factory.MockDeviceService
            .Setup(x => x.GetDevice(deviceId))
            .ReturnsAsync((Device?)null);

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync($"api/devices/{deviceId}/shares", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateShare_ReturnsOk_WithValidUpdate()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/documents",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updateDto = new ShareUpdateDto
        {
            Enabled = false
        };

        var updatedShare = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/documents",
            Enabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockShareService
            .Setup(x => x.GetShare(shareId))
            .ReturnsAsync(share);

        _factory.MockShareService
            .Setup(x => x.UpdateShare(It.IsAny<Share>()))
            .ReturnsAsync(updatedShare);

        var json = JsonSerializer.Serialize(updateDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PutAsync($"api/devices/{deviceId}/shares/{shareId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteShare_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();
        var share = new Share
        {
            Id = shareId,
            DeviceId = deviceId,
            Name = "Documents",
            Path = "/documents",
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _factory.MockShareService
            .Setup(x => x.GetShare(shareId))
            .ReturnsAsync(share);

        _factory.MockShareService
            .Setup(x => x.DeleteShare(shareId))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _httpClient.DeleteAsync($"api/devices/{deviceId}/shares/{shareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteShare_Returns404_WhenShareNotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        _factory.MockShareService
            .Setup(x => x.GetShare(shareId))
            .ReturnsAsync((Share?)null);

        // Act
        var response = await _httpClient.DeleteAsync($"api/devices/{deviceId}/shares/{shareId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
