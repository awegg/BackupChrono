using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BackupChrono.IntegrationTests.Controllers.E2E;

/// <summary>
/// End-to-end integration tests for DevicesController.
/// Tests the complete call stack: HTTP → Controller → DeviceService → GitConfigService → Git Repository
/// Verifies actual persistence and data integrity.
/// </summary>
public class DevicesControllerE2ETests : IAsyncLifetime
{
    private BackupChronoE2EWebApplicationFactory _factory = null!;
    private HttpClient _httpClient = null!;
    private GitConfigService _gitConfigService = null!;

    public async Task InitializeAsync()
    {
        _factory = new BackupChronoE2EWebApplicationFactory();
        await _factory.InitializeAsync();
        
        _httpClient = _factory.CreateClient();
        _gitConfigService = _factory.Services.GetRequiredService<GitConfigService>();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateDevice_PersistsToGitRepository()
    {
        // ARRANGE
        var createDto = new DeviceCreateDto
        {
            Name = "production-nas",
            Protocol = "SMB",
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = "secret",
            WakeOnLanEnabled = false
        };

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ACT - Create device via HTTP
        var createResponse = await _httpClient.PostAsync("api/devices", content);
        
        // DEBUG - if failed, show the error
        if (!createResponse.IsSuccessStatusCode)
        {
            var errorContent = await createResponse.Content.ReadAsStringAsync();
            System.Console.WriteLine($"Error Response: {errorContent}");
        }
        
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        // Extract device ID from location header
        var locationHeader = createResponse.Headers.Location?.ToString() ?? "";
        var deviceId = Guid.Parse(locationHeader.Split('/').Last());

        // VERIFY - Device is actually persisted in Git
        var devicePath = $"devices/{createDto.Name}.yaml";
        var persistedDevice = await _gitConfigService.ReadYamlFile<Device>(devicePath);

        // ASSERT - All properties persisted correctly
        persistedDevice.Should().NotBeNull();
        persistedDevice!.Name.Should().Be(createDto.Name);
        persistedDevice.Protocol.ToString().Should().Be(createDto.Protocol);
        persistedDevice.Host.Should().Be(createDto.Host);
        persistedDevice.Port.Should().Be(createDto.Port);
        persistedDevice.Username.Should().Be(createDto.Username);
    }

    [Fact]
    public async Task GetDevice_ReturnsPersistedData_AfterCreate()
    {
        // ARRANGE - Create a device
        var deviceName = $"device-{Guid.NewGuid()}";
        var createDto = new DeviceCreateDto
        {
            Name = deviceName,
            Protocol = "SSH",
            Host = "example.com",
            Port = 22,
            Username = "ubuntu",
            Password = "password123",
            WakeOnLanEnabled = true,
            WakeOnLanMacAddress = "AA:BB:CC:DD:EE:FF"
        };

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var createResponse = await _httpClient.PostAsync("api/devices", content);
        var deviceId = Guid.Parse(createResponse.Headers.Location?.ToString().Split('/').Last() ?? "");

        // ACT - Retrieve the device
        var getResponse = await _httpClient.GetAsync($"api/devices/{deviceId}");

        // ASSERT - Response contains correct data
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await getResponse.Content.ReadAsStringAsync();
        var json_doc = JsonDocument.Parse(responseContent);
        var root = json_doc.RootElement;

        root.GetProperty("name").GetString().Should().Be(deviceName);
        root.GetProperty("protocol").GetString().Should().Be("SSH");
        root.GetProperty("host").GetString().Should().Be("example.com");
        root.GetProperty("port").GetInt32().Should().Be(22);
        root.GetProperty("wakeOnLanEnabled").GetBoolean().Should().BeTrue();
        root.GetProperty("wakeOnLanMacAddress").GetString().Should().Be("AA:BB:CC:DD:EE:FF");
    }

    [Fact]
    public async Task UpdateDevice_PersistsChangesToGit()
    {
        // ARRANGE - Create initial device
        var deviceName = $"device-{Guid.NewGuid()}";
        var createDto = new DeviceCreateDto
        {
            Name = deviceName,
            Protocol = "SMB",
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = "pass"
        };

        var createJson = JsonSerializer.Serialize(createDto);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _httpClient.PostAsync("api/devices", createContent);
        var deviceId = Guid.Parse(createResponse.Headers.Location?.ToString().Split('/').Last() ?? "");

        // Update the device
        var updateDto = new DeviceUpdateDto
        {
            Host = "192.168.1.200",  // Changed IP
            Port = 4445              // Changed port
        };

        var updateJson = JsonSerializer.Serialize(updateDto);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

        // ACT - Update device
        var updateResponse = await _httpClient.PutAsync($"api/devices/{deviceId}", updateContent);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // VERIFY - Changes persisted to Git
        var devicePath = $"devices/{deviceName}.yaml";
        var persistedDevice = await _gitConfigService.ReadYamlFile<Device>(devicePath);

        // ASSERT - Persisted data reflects updates
        persistedDevice!.Host.Should().Be("192.168.1.200");
        persistedDevice.Port.Should().Be(4445);
        persistedDevice.Name.Should().Be(deviceName);  // Name unchanged
    }

    [Fact]
    public async Task ListDevices_ReturnsAllPersistedDevices()
    {
        // ARRANGE - Create multiple devices
        var device1 = await CreateDeviceViaApi("device1", "SMB", "192.168.1.1");
        var device2 = await CreateDeviceViaApi("device2", "SSH", "192.168.1.2");
        var device3 = await CreateDeviceViaApi("device3", "RSYNC", "192.168.1.3");

        // ACT - List all devices
        var response = await _httpClient.GetAsync("api/devices");

        // ASSERT - All devices returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var devices = json.RootElement.EnumerateArray().ToList();

        devices.Should().HaveCountGreaterThanOrEqualTo(3);
        
        var names = devices.Select(d => d.GetProperty("name").GetString()).ToList();
        names.Should().Contain("device1");
        names.Should().Contain("device2");
        names.Should().Contain("device3");
    }

    [Fact]
    public async Task DeleteDevice_RemovesFromGitRepository()
    {
        // ARRANGE - Create device
        var deviceName = $"device-{Guid.NewGuid()}";
        var deviceId = await CreateDeviceViaApi(deviceName, "SMB", "192.168.1.100");

        // Verify it exists in Git before deletion
        var devicePath = $"devices/{deviceName}.yaml";
        var existingDevice = await _gitConfigService.ReadYamlFile<Device>(devicePath);
        existingDevice.Should().NotBeNull();

        // ACT - Delete device
        var deleteResponse = await _httpClient.DeleteAsync($"api/devices/{deviceId}");

        // ASSERT - Delete successful
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify device removed from Git
        var deletedDevice = await _gitConfigService.ReadYamlFile<Device>(devicePath);
        deletedDevice.Should().BeNull("Device should be removed from Git after deletion");
    }

    [Fact]
    public async Task CreateMultipleDevices_AllPersistIndependently()
    {
        // ARRANGE - Create multiple devices rapidly
        var deviceIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var deviceName = $"concurrent-device-{i}-{Guid.NewGuid()}";
            var createDto = new DeviceCreateDto
            {
                Name = deviceName,
                Protocol = i % 2 == 0 ? "SMB" : "SSH",
                Host = $"192.168.1.{100 + i}",
                Port = 445 + i,
                Username = $"user{i}",
                Password = "password"
            };

            var json = JsonSerializer.Serialize(createDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/devices", content);
            
            var deviceId = Guid.Parse(response.Headers.Location?.ToString().Split('/').Last() ?? "");
            deviceIds.Add(deviceId);
        }

        // ACT - Verify all devices persisted independently
        var listResponse = await _httpClient.GetAsync("api/devices");
        var listContent = await listResponse.Content.ReadAsStringAsync();
        var json_list = JsonDocument.Parse(listContent);
        var devices = json_list.RootElement.EnumerateArray().ToList();

        // ASSERT - All devices have unique data
        devices.Should().HaveCountGreaterThanOrEqualTo(3);
        
        var hosts = devices.Select(d => d.GetProperty("host").GetString()).Distinct().ToList();
        hosts.Should().Contain("192.168.1.100");
        hosts.Should().Contain("192.168.1.101");
        hosts.Should().Contain("192.168.1.102");
    }

    // Helper method to create device via API
    private async Task<Guid> CreateDeviceViaApi(string name, string protocol, string host)
    {
        var createDto = new DeviceCreateDto
        {
            Name = name,
            Protocol = protocol,
            Host = host,
            Port = protocol == "SMB" ? 445 : 22,
            Username = "testuser",
            Password = "testpass"
        };

        var json = JsonSerializer.Serialize(createDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("api/devices", content);
        
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return Guid.Parse(response.Headers.Location?.ToString().Split('/').Last() ?? "");
    }
}
