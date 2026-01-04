using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BackupChrono.IntegrationTests.Scheduling;

/// <summary>
/// Integration tests for Quartz scheduler service to ensure proper DI integration.
/// These tests catch configuration issues that would otherwise only appear at runtime.
/// </summary>
public class QuartzSchedulerTests : IClassFixture<BackupChronoE2EWebApplicationFactory>
{
    private readonly BackupChronoE2EWebApplicationFactory _factory;

    public QuartzSchedulerTests(BackupChronoE2EWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void BackupJob_ShouldBeResolvableFromServiceProvider()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act - Try to resolve BackupJob from DI container
        var backupJob = scope.ServiceProvider.GetRequiredService<BackupChrono.Infrastructure.Scheduling.BackupJob>();

        // Assert - BackupJob should be successfully created with all dependencies (IServiceProvider and ILogger)
        backupJob.Should().NotBeNull("BackupJob must be registered in DI for Quartz job factory to instantiate it");
    }

    [Fact]
    public async Task QuartzScheduler_ShouldStartSuccessfully_WithCustomJobFactory()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var schedulerService = scope.ServiceProvider.GetRequiredService<IQuartzSchedulerService>();

        // Act
        var act = async () => await schedulerService.Start();

        // Assert - Scheduler should start without errors even though BackupJob requires DI
        await act.Should().NotThrowAsync(
            because: "the custom MicrosoftDependencyInjectionJobFactory should be configured to handle dependency injection for Quartz jobs");
    }
}
