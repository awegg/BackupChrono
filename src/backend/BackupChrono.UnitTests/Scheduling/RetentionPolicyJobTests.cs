using BackupChrono.Core.DTOs;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace BackupChrono.UnitTests.Scheduling;

/// <summary>
/// Unit tests for RetentionPolicyJob - focus on error handling and execution flow
/// </summary>
public class RetentionPolicyJobTests
{
    [Fact]
    public async Task Execute_CallsRetentionPolicyService()
    {
        // Arrange
        var mockRetentionService = new Mock<IRetentionPolicyService>();
        mockRetentionService
            .Setup(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetentionRunResult>());

        var mockLogger = new Mock<ILogger<RetentionPolicyJob>>();
        var job = new RetentionPolicyJob(mockRetentionService.Object, mockLogger.Object);
        
        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(x => x.FireInstanceId).Returns("test-job-123");
        mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await job.Execute(mockContext.Object);

        // Assert
        mockRetentionService.Verify(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_LogsErrorAndRethrowsOnException()
    {
        // Arrange
        var mockRetentionService = new Mock<IRetentionPolicyService>();
        mockRetentionService
            .Setup(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var mockLogger = new Mock<ILogger<RetentionPolicyJob>>();
        var job = new RetentionPolicyJob(mockRetentionService.Object, mockLogger.Object);
        
        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(x => x.FireInstanceId).Returns("test-job-456");
        mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<JobExecutionException>(async () =>
        {
            await job.Execute(mockContext.Object);
        });

        mockRetentionService.Verify(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_CompletesWithMixedResults()
    {
        // Arrange
        var results = new List<RetentionRunResult>
        {
            new() { DeviceName = "nas1", ShareName = "share1", SnapshotsPruned = 5, SpaceReclaimedBytes = 1024 * 1024 * 100, Error = null },
            new() { DeviceName = "nas1", ShareName = "share2", SnapshotsPruned = 3, SpaceReclaimedBytes = 1024 * 1024 * 50, Error = null },
            new() { DeviceName = "nas2", ShareName = "share1", SnapshotsPruned = 0, SpaceReclaimedBytes = 0, Error = "Test error" }
        };
        
        var mockRetentionService = new Mock<IRetentionPolicyService>();
        mockRetentionService
            .Setup(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var mockLogger = new Mock<ILogger<RetentionPolicyJob>>();
        var job = new RetentionPolicyJob(mockRetentionService.Object, mockLogger.Object);
        
        var mockContext = new Mock<IJobExecutionContext>();
        mockContext.Setup(x => x.FireInstanceId).Returns("test-job-789");
        mockContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await job.Execute(mockContext.Object);

        // Assert
        mockRetentionService.Verify(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()), Times.Once);
        // Verify job completed without throwing despite having an error result
    }
}
