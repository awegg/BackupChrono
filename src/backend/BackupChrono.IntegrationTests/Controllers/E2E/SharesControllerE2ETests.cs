using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Git;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BackupChrono.IntegrationTests.Controllers.E2E;

/// <summary>
/// End-to-end integration tests for SharesController.
/// Tests persistence of shares with their device relationships.
/// Verifies data integrity through the complete call stack.
/// </summary>
public class SharesControllerE2ETests : IAsyncLifetime
{
    private BackupChronoE2EWebApplicationFactory _factory = null!;
    private HttpClient _httpClient = null!;
    private GitConfigService _gitConfigService = null!;
    private IShareService _shareService = null!;
    private IDeviceService _deviceService = null!;

    public async Task InitializeAsync()
    {
        _factory = new BackupChronoE2EWebApplicationFactory();
        await _factory.InitializeAsync();
        
        _httpClient = _factory.CreateClient();
        _gitConfigService = _factory.Services.GetRequiredService<GitConfigService>();
        _shareService = _factory.Services.GetRequiredService<IShareService>();
        _deviceService = _factory.Services.GetRequiredService<IDeviceService>();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateShare_PersistsToGitRepository_WithDeviceRelationship()
    {
        // ARRANGE - Create device first
        var device = await CreateDeviceViaService("test-device", "SMB", "192.168.1.1");
        var deviceId = device.Id;

        var shareDto = new ShareCreateDto
        {
            Name = "Documents",
            Path = "/documents",
            Enabled = true
        };

        var json = JsonSerializer.Serialize(shareDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ACT - Create share via HTTP
        var response = await _httpClient.PostAsync($"api/devices/{deviceId}/shares", content);

        // ASSERT - Share created
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var shareId = Guid.Parse(response.Headers.Location?.ToString().Split('/').Last() ?? "");

        // VERIFY - Share persisted in Git
        var sharePath = $"shares/{device.Name}/{shareDto.Name}.yaml";
        var persistedShare = await _gitConfigService.ReadYamlFile<Share>(sharePath);

        persistedShare.Should().NotBeNull();
        persistedShare!.Name.Should().Be("Documents");
        persistedShare.Path.Should().Be("/documents");
        persistedShare.DeviceId.Should().Be(deviceId);
        persistedShare.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetShare_ReturnsPersistedData_WithDeviceContext()
    {
        // ARRANGE - Create device and share
        var device = await CreateDeviceViaService("device-shares", "SMB", "192.168.1.100");
        var shareId = await CreateShareViaApi(device.Id, "Backups", "/backups", true);

        // ACT - Get the share
        var response = await _httpClient.GetAsync($"api/devices/{device.Id}/shares/{shareId}");

        // ASSERT - Share data returned correctly
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        root.GetProperty("name").GetString().Should().Be("Backups");
        root.GetProperty("path").GetString().Should().Be("/backups");
        root.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ListShares_ReturnsAllPersistedShares_ForDevice()
    {
        // ARRANGE - Create device and multiple shares
        var device = await CreateDeviceViaService($"multi-share-device-{Guid.NewGuid()}", "SMB", "192.168.1.50");
        
        await CreateShareViaApi(device.Id, "Documents", "/documents", true);
        await CreateShareViaApi(device.Id, "Photos", "/photos", true);
        await CreateShareViaApi(device.Id, "Videos", "/videos", false);

        // ACT - List shares for device
        var response = await _httpClient.GetAsync($"api/devices/{device.Id}/shares");

        // ASSERT - All shares returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var shares = json.RootElement.EnumerateArray().ToList();

        shares.Should().HaveCountGreaterThanOrEqualTo(3);
        
        var names = shares.Select(s => s.GetProperty("name").GetString()).ToList();
        names.Should().Contain("Documents");
        names.Should().Contain("Photos");
        names.Should().Contain("Videos");
    }

    [Fact]
    public async Task UpdateShare_PersistsChangesToGit()
    {
        // ARRANGE - Create device and share
        var device = await CreateDeviceViaService($"update-device-{Guid.NewGuid()}", "SMB", "192.168.1.60");
        var shareId = await CreateShareViaApi(device.Id, "Original", "/original", true);

        var updateDto = new ShareUpdateDto
        {
            Name = "Updated",
            Path = "/updated-path",
            Enabled = false
        };

        var json = JsonSerializer.Serialize(updateDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // ACT - Update share
        var response = await _httpClient.PutAsync($"api/devices/{device.Id}/shares/{shareId}", content);

        // ASSERT - Update successful
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // VERIFY - Changes persisted to Git
        var share = await _shareService.GetShare(shareId);
        share!.Name.Should().Be("Updated");
        share.Path.Should().Be("/updated-path");
        share.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteShare_RemovesFromGitRepository()
    {
        // ARRANGE - Create device and share
        var device = await CreateDeviceViaService($"delete-device-{Guid.NewGuid()}", "SMB", "192.168.1.70");
        var shareId = await CreateShareViaApi(device.Id, "ToDelete", "/to-delete", true);

        // Verify share exists
        var existingShare = await _shareService.GetShare(shareId);
        existingShare.Should().NotBeNull();

        // ACT - Delete share
        var response = await _httpClient.DeleteAsync($"api/devices/{device.Id}/shares/{shareId}");

        // ASSERT - Delete successful
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // VERIFY - Share removed from storage
        var deletedShare = await _shareService.GetShare(shareId);
        deletedShare.Should().BeNull("Share should be removed after deletion");
    }

    [Fact]
    public async Task ShareWithoutDevice_Returns404()
    {
        // ARRANGE - Non-existent device ID
        var nonExistentDeviceId = Guid.NewGuid();
        var nonExistentShareId = Guid.NewGuid();

        // ACT - Try to get share without device
        var response = await _httpClient.GetAsync($"api/devices/{nonExistentDeviceId}/shares/{nonExistentShareId}");

        // ASSERT - 404 returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleSharesOnDevice_AllPersistIndependently()
    {
        // ARRANGE - Create device
        var device = await CreateDeviceViaService($"concurrent-shares-{Guid.NewGuid()}", "SMB", "192.168.1.80");
        var shareIds = new List<Guid>();

        // Create multiple shares concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(i => CreateShareViaApi(device.Id, $"Share{i}", $"/share{i}", i % 2 == 0))
            .ToList();

        var createdIds = await Task.WhenAll(tasks);

        // ACT - Retrieve all shares
        var response = await _httpClient.GetAsync($"api/devices/{device.Id}/shares");

        // ASSERT - All shares persisted independently
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var shares = json.RootElement.EnumerateArray().ToList();

        shares.Should().HaveCountGreaterThanOrEqualTo(5);
        
        for (int i = 0; i < 5; i++)
        {
            var name = $"Share{i}";
            shares.Should().Contain(s => s.GetProperty("name").GetString() == name);
        }
    }

    // Helper methods
    private async Task<Device> CreateDeviceViaService(string name, string protocol, string host)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = name,
            Protocol = Enum.Parse<ProtocolType>(protocol),
            Host = host,
            Port = protocol == "SMB" ? 445 : 22,
            Username = "testuser",
            Password = new EncryptedCredential("testpass"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _deviceService.CreateDevice(device);
    }

    private async Task<Guid> CreateShareViaApi(Guid deviceId, string name, string path, bool enabled)
    {
        var shareDto = new ShareCreateDto
        {
            Name = name,
            Path = path,
            Enabled = enabled
        };

        var json = JsonSerializer.Serialize(shareDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"api/devices/{deviceId}/shares", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return Guid.Parse(response.Headers.Location?.ToString().Split('/').Last() ?? "");
    }
}
