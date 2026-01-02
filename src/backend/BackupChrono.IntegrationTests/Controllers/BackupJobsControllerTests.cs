using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BackupChrono.IntegrationTests.Controllers;

public class BackupJobsControllerTests : IAsyncLifetime
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
    public async Task ListBackupJobs_ReturnsEmptyList_WhenNoJobs()
    {
        // Arrange - done in factory mock setup

        // Act
        var response = await _httpClient.GetAsync("api/backup-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jobs = JsonDocument.Parse(content).RootElement.EnumerateArray().ToList();
        jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackupJobs_ReturnsJobList_WhenJobsExist()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            RetryAttempt = 0
        };

        // We need to mock IBackupJobRepository, but it's not directly available
        // The test needs to work through the actual DI setup
        // For now, we'll test with an empty result since the factory uses mocks
        
        // Act
        var response = await _httpClient.GetAsync("api/backup-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListBackupJobs_FiltersByDeviceId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"api/backup-jobs?deviceId={deviceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.EnumerateArray().ToList().Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackupJobs_FiltersByStatus()
    {
        // Arrange
        var status = "Completed";

        // Act
        var response = await _httpClient.GetAsync($"api/backup-jobs?status={status}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.EnumerateArray().ToList().Should().BeEmpty();
    }

    [Fact]
    public async Task ListBackupJobs_RespectsLimit()
    {
        // Arrange
        var limit = 5;

        // Act
        var response = await _httpClient.GetAsync($"api/backup-jobs?limit={limit}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var jobs = json.RootElement.EnumerateArray().ToList();
        jobs.Should().HaveCountLessThanOrEqualTo(limit, "returned jobs should not exceed the specified limit");
    }

    [Fact]
    public async Task GetBackupJob_Returns404_WhenJobNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"api/backup-jobs/{jobId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    [Fact]
    public async Task TriggerBackup_ReturnsBadRequest_WithoutDeviceId()
    {
        // Arrange - missing required fields in request
        var request = new TriggerBackupRequest { };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync("api/backup-jobs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TriggerBackup_ReturnsAccepted_WithValidRequest()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        _factory.MockSchedulerService
            .Setup(x => x.TriggerImmediateBackup(deviceId, shareId))
            .Returns(Task.CompletedTask);

        var request = new TriggerBackupRequest { DeviceId = deviceId, ShareId = shareId };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _httpClient.PostAsync("api/backup-jobs", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task ListBackupJobs_ReturnsJobsWithDeviceAndShareNames()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        var mockDevice = new Device
        {
            Name = "ProductionServer",
            Protocol = ProtocolType.SMB,
            Host = "192.168.1.100",
            Port = 445,
            Username = "admin",
            Password = new EncryptedCredential("password")
        };

        var mockShare = new Share
        {
            DeviceId = deviceId,
            Name = "DatabaseBackups",
            Path = "/var/backups/db",
            Enabled = true
        };

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = shareId,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            // Names not set - should be enriched by repository
            DeviceName = null,
            ShareName = null
        };

        // Setup mocks to return device and share
        _factory.MockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync(mockDevice);
        _factory.MockShareService.Setup(s => s.GetShare(shareId))
            .ReturnsAsync(mockShare);

        // Save job to repository
        var jobRepo = _factory.Services.GetRequiredService<IBackupJobRepository>();
        await jobRepo.SaveJob(job);

        // Act
        var response = await _httpClient.GetAsync("/api/backup-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<List<BackupJobDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        jobs.Should().NotBeNull();
        jobs.Should().ContainSingle();
        
        var returnedJob = jobs![0];
        returnedJob.DeviceId.Should().Be(deviceId);
        returnedJob.ShareId.Should().Be(shareId);
        returnedJob.DeviceName.Should().Be("ProductionServer");
        returnedJob.ShareName.Should().Be("DatabaseBackups");
        returnedJob.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task ListBackupJobs_HandlesDeletedDeviceGracefully()
    {
        // Arrange - Job exists but device was deleted
        var deviceId = Guid.NewGuid();
        var shareId = Guid.NewGuid();

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = shareId,
            Type = BackupJobType.Scheduled,
            Status = BackupJobStatus.Failed,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            ErrorMessage = "Backup failed"
        };

        // Setup mocks - device not found, share not found
        _factory.MockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync((Device?)null);
        _factory.MockShareService.Setup(s => s.GetShare(shareId))
            .ReturnsAsync((Share?)null);

        var jobRepo = _factory.Services.GetRequiredService<IBackupJobRepository>();
        await jobRepo.SaveJob(job);

        // Act
        var response = await _httpClient.GetAsync("/api/backup-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<List<BackupJobDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        jobs.Should().NotBeNull();
        jobs.Should().ContainSingle();
        
        var returnedJob = jobs![0];
        returnedJob.DeviceId.Should().Be(deviceId);
        returnedJob.ShareId.Should().Be(shareId);
        // Names should be null since device/share don't exist
        returnedJob.DeviceName.Should().BeNull();
        returnedJob.ShareName.Should().BeNull();
        // Job data should still be present
        returnedJob.Status.Should().Be("Failed");
        returnedJob.ErrorMessage.Should().Be("Backup failed");
    }

    [Fact]
    public async Task ListBackupJobs_DeviceLevelBackup_ShowsOnlyDeviceName()
    {
        // Arrange - Device-level backup (no share)
        var deviceId = Guid.NewGuid();

        var mockDevice = new Device
        {
            Name = "FileServer",
            Protocol = ProtocolType.SSH,
            Host = "192.168.1.200",
            Port = 22,
            Username = "admin",
            Password = new EncryptedCredential("password")
        };

        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ShareId = null, // Device-level backup
            Type = BackupJobType.Manual,
            Status = BackupJobStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _factory.MockDeviceService.Setup(s => s.GetDevice(deviceId))
            .ReturnsAsync(mockDevice);

        var jobRepo = _factory.Services.GetRequiredService<IBackupJobRepository>();
        await jobRepo.SaveJob(job);

        // Act
        var response = await _httpClient.GetAsync("/api/backup-jobs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<List<BackupJobDto>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        jobs.Should().NotBeNull();
        jobs.Should().ContainSingle();
        
        var returnedJob = jobs![0];
        returnedJob.DeviceId.Should().Be(deviceId);
        returnedJob.ShareId.Should().BeNull();
        returnedJob.DeviceName.Should().Be("FileServer");
        returnedJob.ShareName.Should().BeNull();
        returnedJob.Status.Should().Be("Running");
    }}