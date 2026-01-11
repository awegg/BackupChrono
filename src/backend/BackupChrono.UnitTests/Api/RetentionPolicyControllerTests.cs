using BackupChrono.Api.Controllers;
using BackupChrono.Core.DTOs;
using BackupChrono.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Api;

public class RetentionPolicyControllerTests
{
    private readonly Mock<IRetentionPolicyService> _mockService;
    private readonly Mock<ILogger<RetentionPolicyController>> _mockLogger;
    private readonly RetentionPolicyController _controller;

    public RetentionPolicyControllerTests()
    {
        _mockService = new Mock<IRetentionPolicyService>();
        _mockLogger = new Mock<ILogger<RetentionPolicyController>>();
        _controller = new RetentionPolicyController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteRetentionPolicy_ReturnsOk_WithResults()
    {
        var results = new List<RetentionRunResult>
        {
            new() { DeviceName = "nas1", ShareName = "share1", SnapshotsPruned = 5, SpaceReclaimedBytes = 1024 }
        };
        
        _mockService.Setup(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _controller.ExecuteRetentionPolicy(false);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResults = okResult.Value.Should().BeAssignableTo<IEnumerable<RetentionRunResult>>().Subject;
        returnedResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteRetentionPolicy_WithDryRun_PassesDryRunFlag()
    {
        _mockService.Setup(x => x.ApplyForAll(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetentionRunResult>());

        await _controller.ExecuteRetentionPolicy(dryRun: true);

        _mockService.Verify(x => x.ApplyForAll(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteRetentionPolicy_Returns499_OnCancellation()
    {
        _mockService.Setup(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _controller.ExecuteRetentionPolicy(false);

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(499);
    }

    [Fact]
    public async Task ExecuteRetentionPolicy_Returns500_OnError()
    {
        _mockService.Setup(x => x.ApplyForAll(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var result = await _controller.ExecuteRetentionPolicy(false);

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task ExecuteRetentionPolicyForDevice_ReturnsOk_WithResults()
    {
        var results = new List<RetentionRunResult>
        {
            new() { DeviceName = "nas1", ShareName = "share1", SnapshotsPruned = 3, SpaceReclaimedBytes = 512 }
        };
        
        _mockService.Setup(x => x.ApplyForDevice("nas1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _controller.ExecuteRetentionPolicyForDevice("nas1", false);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<IEnumerable<RetentionRunResult>>();
    }

    [Fact]
    public async Task ExecuteRetentionPolicyForDevice_ReturnsNotFound_WhenNoResults()
    {
        _mockService.Setup(x => x.ApplyForDevice("missing", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RetentionRunResult>());

        var result = await _controller.ExecuteRetentionPolicyForDevice("missing", false);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetLastRun_ReturnsOk_WhenDataExists()
    {
        var lastRun = new RetentionLastRun
        {
            Timestamp = DateTime.UtcNow,
            TotalSnapshotsPruned = 10,
            TotalSpaceReclaimedBytes = 2048
        };
        
        _mockService.Setup(x => x.GetLastRun()).ReturnsAsync(lastRun);

        var result = await _controller.GetLastRun();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<RetentionLastRun>();
    }

    [Fact]
    public async Task GetLastRun_ReturnsOk_WhenNull()
    {
        _mockService.Setup(x => x.GetLastRun()).ReturnsAsync((RetentionLastRun?)null);

        var result = await _controller.GetLastRun();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeNull();
    }
}
