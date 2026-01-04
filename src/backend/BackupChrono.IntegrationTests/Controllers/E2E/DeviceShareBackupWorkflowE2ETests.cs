using System.Net;
using System.Net.Http.Json;
using BackupChrono.Api.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackupChrono.IntegrationTests.Controllers.E2E;

/// <summary>
/// End-to-end integration tests for the complete device → share → backup → cleanup workflow.
/// Tests the full lifecycle: create device, add share, trigger backup, remove share, remove device.
/// </summary>
public class DeviceShareBackupWorkflowE2ETests : IClassFixture<BackupChronoE2EWebApplicationFactory>
{
    private readonly BackupChronoE2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeviceShareBackupWorkflowE2ETests(BackupChronoE2EWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteWorkflow_CreateDeviceAddShareRemoveShareRemoveDevice_Success()
    {
        // ARRANGE - Test data
        var deviceName = $"test-device-{Guid.NewGuid():N}";
        var shareName = $"test-share-{Guid.NewGuid():N}";
        
        var createDeviceDto = new DeviceCreateDto
        {
            Name = deviceName,
            Protocol = "SMB",
            Host = "192.168.1.100",
            Port = 445,
            Username = "testuser",
            Password = "testpass123",
            WakeOnLanEnabled = false
        };

        // ACT & ASSERT - Step 1: Create Device
        var createDeviceResponse = await _client.PostAsJsonAsync("/api/devices", createDeviceDto);
        createDeviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdDevice = await createDeviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        createdDevice.Should().NotBeNull();
        createdDevice!.Id.Should().NotBeEmpty();
        createdDevice!.Name.Should().Be(deviceName);
        
        var deviceId = createdDevice.Id;

        // Step 2: Verify device exists in list
        var listDevicesResponse = await _client.GetAsync("/api/devices");
        listDevicesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var devices = await listDevicesResponse.Content.ReadFromJsonAsync<List<DeviceDto>>();
        devices.Should().NotBeNull();
        devices.Should().Contain(d => d.Id == deviceId);

        // Step 3: Add Share to Device
        var createShareDto = new ShareCreateDto
        {
            Name = shareName,
            Path = "/mnt/data",
            Enabled = true,
            Schedule = new ScheduleDto
            {
                CronExpression = "0 2 * * *"
            }
        };

        var createShareResponse = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/shares", createShareDto);
        createShareResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdShare = await createShareResponse.Content.ReadFromJsonAsync<ShareDto>();
        createdShare.Should().NotBeNull();
        createdShare!.Id.Should().NotBeEmpty();
        createdShare.Name.Should().Be(shareName);
        createdShare.DeviceId.Should().Be(deviceId);
        
        var shareId = createdShare.Id;

        // Step 4: Verify share exists in device's share list
        var listSharesResponse = await _client.GetAsync($"/api/devices/{deviceId}/shares");
        listSharesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var shares = await listSharesResponse.Content.ReadFromJsonAsync<List<ShareDto>>();
        shares.Should().NotBeNull();
        shares.Should().ContainSingle();
        shares![0].Id.Should().Be(shareId);

        // Step 5: Trigger Manual Backup for the share (accept that it's queued)
        var triggerBackupDto = new TriggerBackupRequest
        {
            DeviceId = deviceId,
            ShareId = shareId
        };

        var triggerBackupResponse = await _client.PostAsJsonAsync("/api/backup-jobs", triggerBackupDto);
        triggerBackupResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var triggerResult = await triggerBackupResponse.Content.ReadFromJsonAsync<TriggerBackupResponse>();
        triggerResult.Should().NotBeNull();
        triggerResult!.Message.Should().Contain("triggered");

        // Step 6: Trigger Device-Level Backup (all shares) - also just verify it's accepted
        var triggerDeviceBackupDto = new TriggerBackupRequest
        {
            DeviceId = deviceId,
            ShareId = null // null means all shares
        };

        var triggerDeviceBackupResponse = await _client.PostAsJsonAsync("/api/backup-jobs", triggerDeviceBackupDto);
        triggerDeviceBackupResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Step 7: Delete the Share
        var deleteShareResponse = await _client.DeleteAsync($"/api/devices/{deviceId}/shares/{shareId}");
        deleteShareResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 8: Verify share is deleted
        var getShareResponse = await _client.GetAsync($"/api/devices/{deviceId}/shares/{shareId}");
        getShareResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listSharesAfterDeleteResponse = await _client.GetAsync($"/api/devices/{deviceId}/shares");
        var sharesAfterDelete = await listSharesAfterDeleteResponse.Content.ReadFromJsonAsync<List<ShareDto>>();
        sharesAfterDelete.Should().NotBeNull();
        sharesAfterDelete.Should().BeEmpty();

        // Step 9: Delete the Device
        var deleteDeviceResponse = await _client.DeleteAsync($"/api/devices/{deviceId}");
        deleteDeviceResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 10: Verify device is deleted
        var getDeviceResponse = await _client.GetAsync($"/api/devices/{deviceId}");
        getDeviceResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listDevicesAfterDeleteResponse = await _client.GetAsync("/api/devices");
        var devicesAfterDelete = await listDevicesAfterDeleteResponse.Content.ReadFromJsonAsync<List<DeviceDto>>();
        devicesAfterDelete.Should().NotBeNull();
        devicesAfterDelete.Should().NotContain(d => d.Id == deviceId);
    }

    [Fact]
    public async Task CompleteWorkflow_CreateMultipleSharesTriggerBackupsCleanup_Success()
    {
        // ARRANGE - Create device
        var deviceName = $"multi-share-device-{Guid.NewGuid():N}";
        var createDeviceDto = new DeviceCreateDto
        {
            Name = deviceName,
            Protocol = "SSH",
            Host = "192.168.1.200",
            Port = 22,
            Username = "admin",
            Password = "secure123",
            WakeOnLanEnabled = false
        };

        var createDeviceResponse = await _client.PostAsJsonAsync("/api/devices", createDeviceDto);
        createDeviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdDevice = await createDeviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        createdDevice.Should().NotBeNull();
        var deviceId = createdDevice!.Id;

        // ACT - Create multiple shares
        var share1Dto = new ShareCreateDto
        {
            Name = "documents",
            Path = "/home/user/documents",
            Enabled = true,
            Schedule = new ScheduleDto { CronExpression = "0 3 * * *" }
        };

        var share2Dto = new ShareCreateDto
        {
            Name = "photos",
            Path = "/home/user/photos",
            Enabled = true,
            Schedule = new ScheduleDto { CronExpression = "0 4 * * *" }
        };

        var share3Dto = new ShareCreateDto
        {
            Name = "videos",
            Path = "/home/user/videos",
            Enabled = false, // Disabled share
            Schedule = null
        };

        var share1Response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/shares", share1Dto);
        share1Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var share2Response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/shares", share2Dto);
        share2Response.StatusCode.Should().Be(HttpStatusCode.Created);
        var share3Response = await _client.PostAsJsonAsync($"/api/devices/{deviceId}/shares", share3Dto);
        share3Response.StatusCode.Should().Be(HttpStatusCode.Created);

        var share1 = await share1Response.Content.ReadFromJsonAsync<ShareDto>();
        var share2 = await share2Response.Content.ReadFromJsonAsync<ShareDto>();
        var share3 = await share3Response.Content.ReadFromJsonAsync<ShareDto>();

        // ASSERT - Verify all shares created
        var listSharesResponse = await _client.GetAsync($"/api/devices/{deviceId}/shares");
        var shares = await listSharesResponse.Content.ReadFromJsonAsync<List<ShareDto>>();
        shares.Should().HaveCount(3);

        // ACT - Trigger backup for specific shares - just verify they're accepted
        var trigger1Response = await _client.PostAsJsonAsync("/api/backup-jobs", new TriggerBackupRequest 
        { 
            DeviceId = deviceId, 
            ShareId = share1!.Id 
        });
        trigger1Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var trigger2Response = await _client.PostAsJsonAsync("/api/backup-jobs", new TriggerBackupRequest 
        { 
            DeviceId = deviceId, 
            ShareId = share2!.Id 
        });
        trigger2Response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Note: Without a real backup system, jobs won't be created immediately in tests
        // The important part is that the trigger requests are accepted

        // CLEANUP - Delete shares one by one
        var delete1 = await _client.DeleteAsync($"/api/devices/{deviceId}/shares/{share1.Id}");
        delete1.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var delete2 = await _client.DeleteAsync($"/api/devices/{deviceId}/shares/{share2.Id}");
        delete2.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var delete3 = await _client.DeleteAsync($"/api/devices/{deviceId}/shares/{share3!.Id}");
        delete3.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Verify all shares deleted
        var sharesAfterDelete = await (await _client.GetAsync($"/api/devices/{deviceId}/shares"))
            .Content.ReadFromJsonAsync<List<ShareDto>>();
        sharesAfterDelete.Should().BeEmpty();

        // Delete device
        var deleteResponse = await _client.DeleteAsync($"/api/devices/{deviceId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Workflow_UpdateShareThenBackup_Success()
    {
        // ARRANGE - Create device and share
        var deviceDto = new DeviceCreateDto
        {
            Name = $"update-test-{Guid.NewGuid():N}",
            Protocol = "SMB",
            Host = "192.168.1.50",
            Port = 445,
            Username = "user",
            Password = "pass"
        };

        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", deviceDto);
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        device.Should().NotBeNull();

        var shareDto = new ShareCreateDto
        {
            Name = "initial-share",
            Path = "/data",
            Enabled = true
        };

        var shareResponse = await _client.PostAsJsonAsync($"/api/devices/{device!.Id}/shares", shareDto);
        shareResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var share = await shareResponse.Content.ReadFromJsonAsync<ShareDto>();

        // ACT - Update share
        var updateDto = new ShareUpdateDto
        {
            Name = "updated-share",
            Path = "/data/updated",
            Enabled = true,
            Schedule = new ScheduleDto { CronExpression = "0 5 * * *" }
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/devices/{device.Id}/shares/{share!.Id}", updateDto);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedShare = await updateResponse.Content.ReadFromJsonAsync<ShareDto>();
        updatedShare!.Name.Should().Be("updated-share");
        updatedShare.Path.Should().Be("/data/updated");

        // Trigger backup with updated share - verify accepted
        var triggerResponse = await _client.PostAsJsonAsync("/api/backup-jobs", new TriggerBackupRequest
        {
            DeviceId = device.Id,
            ShareId = share.Id
        });
        triggerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Note: Without a real backup system, jobs won't be created immediately in tests
        // The important part is that the configuration flow works end-to-end

        // CLEANUP
        await _client.DeleteAsync($"/api/devices/{device.Id}/shares/{share.Id}");
        await _client.DeleteAsync($"/api/devices/{device.Id}");
    }
}

// DTOs for the tests
public class TriggerBackupResponse
{
    public string Message { get; set; } = string.Empty;
}
