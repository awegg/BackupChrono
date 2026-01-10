using System.Net;
using System.Net.Http.Json;
using BackupChrono.Core.DTOs;
using BackupChrono.Api.DTOs; // Added namespace for DeviceDto
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces; // Added namespace
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit;

namespace BackupChrono.IntegrationTests.Scheduling;

public class SchedulingFlowTests : IClassFixture<BackupChronoE2EWebApplicationFactory>
{
    private readonly BackupChronoE2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SchedulingFlowTests(BackupChronoE2EWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDevice_WithSchedule_ShouldScheduleBackupJob()
    {
        // Arrange
        var createDeviceDto = new
        {
            Name = "scheduled-device-" + Guid.NewGuid(),
            Protocol = "SMB",
            Host = "localhost",
            Username = "user",
            Password = "password",
            Schedule = new { CronExpression = "0 30 4 * * ?" } // 4:30 AM
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/devices", createDeviceDto);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new Exception($"Failed to create device. Status: {response.StatusCode}. Error: {error?.Error}. Detail: {error?.Detail}");
        }
        var createdDevice = await response.Content.ReadFromJsonAsync<DeviceDto>();
        createdDevice.Should().NotBeNull();

        // Assert - Check Quartz Scheduler directly
        using var scope = _factory.Services.CreateScope();
        var schedulerService = scope.ServiceProvider.GetRequiredService<IQuartzSchedulerService>();
        
        // Cast to concrete implementation to access internal property
        var concreteService = schedulerService as QuartzSchedulerService;
        concreteService.Should().NotBeNull("Service should be of type QuartzSchedulerService");
        
        var schedulerInstance = concreteService!.Scheduler;
        schedulerInstance.Should().NotBeNull("Scheduler should be initialized");
        
        schedulerInstance.Should().NotBeNull("Scheduler instance should be initialized");
        
        var jobKey = new JobKey($"device-{createdDevice!.Id}", "backups");
        var exists = await schedulerInstance.CheckExists(jobKey);
        
        exists.Should().BeTrue("Backup job should be scheduled for the new device");
        
        var triggerKey = new TriggerKey($"device-{createdDevice.Id}-trigger", "backups");
        var trigger = await schedulerInstance.GetTrigger(triggerKey);
        trigger.Should().NotBeNull();
        
        var cronTrigger = trigger as ICronTrigger;
        cronTrigger.Should().NotBeNull("Trigger should be a cron trigger");
        cronTrigger.CronExpressionString.Should().Be("0 30 4 * * ?");    }

    [Fact]
    public async Task UpdateDevice_Schedule_ShouldUpdateBackupJob()
    {
        // Arrange - Create device
        var createDeviceDto = new
        {
            Name = "update-sched-device-" + Guid.NewGuid(),
            Protocol = "SMB",
            Host = "localhost",
            Username = "user",
            Password = "password",
            Schedule = new { CronExpression = "0 0 12 * * ?" } 
        };
        
        var createResp = await _client.PostAsJsonAsync("/api/devices", createDeviceDto);
        var createdDevice = await createResp.Content.ReadFromJsonAsync<DeviceDto>();
        createdDevice.Should().NotBeNull();
        
        // Act - Update schedule
        var updateDto = new
        {
            createDeviceDto.Name,
            createDeviceDto.Protocol,
            createDeviceDto.Host,
            createDeviceDto.Username,
            createDeviceDto.Password,
            Schedule = new { CronExpression = "0 0 18 * * ?" } // Changed to 6 PM
        };
        
        var updateResp = await _client.PutAsJsonAsync($"/api/devices/{createdDevice!.Id}", updateDto);
        if (updateResp.StatusCode != HttpStatusCode.OK)
        {
            var error = await updateResp.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new Exception($"Failed to update device. Status: {updateResp.StatusCode}. Error: {error?.Error}. Detail: {error?.Detail}");
        }

        // Assert
        using var scope = _factory.Services.CreateScope();
        var schedulerService = scope.ServiceProvider.GetRequiredService<IQuartzSchedulerService>();
        
        var concreteService = schedulerService as QuartzSchedulerService;
        var schedulerInstance = concreteService!.Scheduler;
        
        var triggerKey = new TriggerKey($"device-{createdDevice!.Id}-trigger", "backups");
        var trigger = await schedulerInstance.GetTrigger(triggerKey) as ICronTrigger;
        
        trigger.Should().NotBeNull("Trigger should be a cron trigger");
        trigger.CronExpressionString.Should().Be("0 0 18 * * ?");    }

    [Fact]
    public async Task DeleteDevice_ShouldUnscheduleBackupJob()
    {
        // Arrange
        var createDeviceDto = new
        {
            Name = "delete-sched-device-" + Guid.NewGuid(),
            Protocol = "SMB",
            Host = "localhost",
            Username = "user",
            Password = "password",
            Schedule = new { CronExpression = "0 0 12 * * ?" } 
        };
        
        var createResp = await _client.PostAsJsonAsync("/api/devices", createDeviceDto);
        var createdDevice = await createResp.Content.ReadFromJsonAsync<DeviceDto>();
        createdDevice.Should().NotBeNull();
        
        // Act
        var deleteResp = await _client.DeleteAsync($"/api/devices/{createdDevice!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert
        using var scope = _factory.Services.CreateScope();
        var schedulerService = scope.ServiceProvider.GetRequiredService<IQuartzSchedulerService>();
        
        var concreteService = schedulerService as QuartzSchedulerService;
        var schedulerInstance = concreteService!.Scheduler;
        
        var jobKey = new JobKey($"device-{createdDevice!.Id}", "backups");
        var exists = await schedulerInstance.CheckExists(jobKey);
        
        exists.Should().BeFalse("Backup job should be unscheduled after device deletion");
    }
}
