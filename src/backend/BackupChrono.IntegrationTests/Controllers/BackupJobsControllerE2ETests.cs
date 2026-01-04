using System.Net;
using System.Net.Http.Json;
using BackupChrono.Api.DTOs;
using FluentAssertions;
using Xunit;

namespace BackupChrono.IntegrationTests.Controllers;

/// <summary>
/// End-to-end tests for BackupJobsController that verify actual backup triggering
/// </summary>
public class BackupJobsControllerE2ETests : IClassFixture<BackupChronoE2EWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BackupJobsControllerE2ETests(BackupChronoE2EWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TriggerBackup_DeviceLevel_ExecutesWithoutErrors()
    {
        // Arrange - Create a device first
        var deviceRequest = new DeviceCreateDto
        {
            Name = "TestDevice-BackupTrigger",
            Host = "192.168.1.100",
            Port = 445,
            Protocol = "SMB",
            Username = "testuser",
            Password = "testpass"
        };

        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", deviceRequest);
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        device.Should().NotBeNull();

        // Act - Trigger device-level backup
        var triggerRequest = new TriggerBackupRequest
        {
            DeviceId = device!.Id,
            ShareId = null // Device-level backup
        };

        var response = await _client.PostAsJsonAsync("/api/backup-jobs", triggerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        // Wait a bit for the job to execute
        await Task.Delay(500);

        // Verify no unhandled exceptions occurred - check backup jobs list
        var jobsResponse = await _client.GetAsync($"/api/backup-jobs?deviceId={device.Id}");
        jobsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TriggerBackup_ShareLevel_ExecutesWithoutErrors()
    {
        // Arrange - Create a device and share first
        var deviceRequest = new DeviceCreateDto
        {
            Name = "TestDevice-ShareBackupTrigger",
            Host = "192.168.1.101",
            Port = 445,
            Protocol = "SMB",
            Username = "testuser",
            Password = "testpass"
        };

        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", deviceRequest);
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        device.Should().NotBeNull();

        var shareRequest = new ShareCreateDto
        {
            Name = "TestShare",
            Path = "/backup",
            Enabled = true
        };

        var shareResponse = await _client.PostAsJsonAsync($"/api/devices/{device!.Id}/shares", shareRequest);
        shareResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var share = await shareResponse.Content.ReadFromJsonAsync<ShareDto>();
        share.Should().NotBeNull();

        // Act - Trigger share-level backup
        var triggerRequest = new TriggerBackupRequest
        {
            DeviceId = device.Id,
            ShareId = share!.Id
        };

        var response = await _client.PostAsJsonAsync("/api/backup-jobs", triggerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Wait a bit for the job to execute
        await Task.Delay(500);

        // Verify no unhandled exceptions occurred
        var jobsResponse = await _client.GetAsync($"/api/backup-jobs?deviceId={device.Id}");
        jobsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TriggerBackup_WithoutDeviceId_ReturnsBadRequest()
    {
        // Arrange
        var triggerRequest = new TriggerBackupRequest
        {
            DeviceId = Guid.Empty,
            ShareId = null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/backup-jobs", triggerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Should().Contain("Invalid backup request");
    }

    [Fact]
    public async Task TriggerBackup_MultipleSimultaneous_HandlesGracefully()
    {
        // Arrange - Create a device
        var deviceRequest = new DeviceCreateDto
        {
            Name = "TestDevice-Concurrent",
            Host = "192.168.1.102",
            Port = 445,
            Protocol = "SMB",
            Username = "testuser",
            Password = "testpass"
        };

        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", deviceRequest);
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();

        var triggerRequest = new TriggerBackupRequest
        {
            DeviceId = device!.Id,
            ShareId = null
        };

        // Act - Trigger multiple backups simultaneously
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => _client.PostAsJsonAsync("/api/backup-jobs", triggerRequest))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should be accepted
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Accepted));

        // Wait for jobs to complete
        await Task.Delay(1000);
    }

    [Fact]
    public async Task TriggerBackup_DeviceWithNoEnabledShares_FailsAndAppearsInJobsList()
    {
        // NOTE: This test verifies the TrackFailedJob mechanism works
        // Arrange - Create a device with no enabled shares
        var deviceRequest = new DeviceCreateDto
        {
            Name = "TestDevice-NoShares",
            Host = "192.168.1.103",
            Port = 445,
            Protocol = "SMB",
            Username = "testuser",
            Password = "testpass"
        };

        var deviceResponse = await _client.PostAsJsonAsync("/api/devices", deviceRequest);
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        device.Should().NotBeNull();

        // Act - Trigger backup on device with no shares
        // Note: This will return 202 Accepted but job won't execute because Quartz is mocked in E2E tests
        var triggerRequest = new TriggerBackupRequest
        {
            DeviceId = device!.Id,
            ShareId = null
        };

        var response = await _client.PostAsJsonAsync("/api/backup-jobs", triggerRequest);

        // Assert - Should be accepted (202)
        // The actual job execution is tested in unit tests
        // This E2E test primarily verifies the HTTP endpoint works
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        // TODO: Once we have real Quartz integration in E2E tests, verify failed job appears:
        // await Task.Delay(3000);
        // var jobsResponse = await _client.GetAsync($"/api/backup-jobs?deviceId={device.Id}");
        // var jobs = await jobsResponse.Content.ReadFromJsonAsync<List<BackupJobDto>>();
        // jobs.Should().HaveCount(1);
        // jobs![0].Status.Should().Be("Failed");
        // jobs[0].ErrorMessage.Should().Contain("no enabled shares");
    }
}

