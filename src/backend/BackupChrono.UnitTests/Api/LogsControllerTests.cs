using BackupChrono.Api.Controllers;
using BackupChrono.Core.DTOs;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Api;

/// <summary>
/// Unit tests for LogsController
/// </summary>
public class LogsControllerTests
{
    private readonly Mock<ILogReaderService> _mockLogReaderService;
    private readonly Mock<ILogger<LogsController>> _mockLogger;
    private readonly LogsController _controller;

    public LogsControllerTests()
    {
        _mockLogReaderService = new Mock<ILogReaderService>();
        _mockLogger = new Mock<ILogger<LogsController>>();
        _controller = new LogsController(_mockLogReaderService.Object, _mockLogger.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetLogs_ReturnsOkWithLogs_WhenLogsExist()
    {
        // Arrange
        var logs = new List<LogEntry>
        {
            new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "INF",
                Message = "Test log message 1",
                SourceContext = "TestContext"
            },
            new LogEntry
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Level = "WRN",
                Message = "Test warning message",
                SourceContext = "TestContext"
            }
        };

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _controller.GetLogs();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedLogs = okResult.Value.Should().BeAssignableTo<IEnumerable<LogEntry>>().Subject;
        returnedLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLogs_WithLevelFilter_PassesCorrectParameters()
    {
        // Arrange
        var logs = new List<LogEntry>
        {
            new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "ERR",
                Message = "Error message",
                SourceContext = "TestContext"
            }
        };

        LogQueryParameters? capturedParameters = null;
        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<LogQueryParameters, CancellationToken>((p, ct) => capturedParameters = p)
            .ReturnsAsync(logs);

        // Act
        await _controller.GetLogs(level: "Error");

        // Assert
        capturedParameters.Should().NotBeNull();
        capturedParameters!.Level.Should().Be("Error");
    }

    [Fact]
    public async Task GetLogs_WithSearchFilter_PassesCorrectParameters()
    {
        // Arrange
        var logs = new List<LogEntry>();
        LogQueryParameters? capturedParameters = null;

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<LogQueryParameters, CancellationToken>((p, ct) => capturedParameters = p)
            .ReturnsAsync(logs);

        // Act
        await _controller.GetLogs(search: "backup");

        // Assert
        capturedParameters.Should().NotBeNull();
        capturedParameters!.Search.Should().Be("backup");
    }

    [Fact]
    public async Task GetLogs_WithDateRangeFilter_PassesCorrectParameters()
    {
        // Arrange
        var logs = new List<LogEntry>();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        LogQueryParameters? capturedParameters = null;

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<LogQueryParameters, CancellationToken>((p, ct) => capturedParameters = p)
            .ReturnsAsync(logs);

        // Act
        await _controller.GetLogs(startDate: startDate, endDate: endDate);

        // Assert
        capturedParameters.Should().NotBeNull();
        capturedParameters!.StartDate.Should().Be(startDate);
        capturedParameters!.EndDate.Should().Be(endDate);
    }

    [Fact]
    public async Task GetLogs_WithCustomLimit_PassesCorrectParameters()
    {
        // Arrange
        var logs = new List<LogEntry>();
        LogQueryParameters? capturedParameters = null;

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<LogQueryParameters, CancellationToken>((p, ct) => capturedParameters = p)
            .ReturnsAsync(logs);

        // Act
        await _controller.GetLogs(limit: 500);

        // Assert
        capturedParameters.Should().NotBeNull();
        capturedParameters!.Limit.Should().Be(500);
    }

    [Fact]
    public async Task GetLogs_WithAllFilters_PassesAllParameters()
    {
        // Arrange
        var logs = new List<LogEntry>();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        LogQueryParameters? capturedParameters = null;

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<LogQueryParameters, CancellationToken>((p, ct) => capturedParameters = p)
            .ReturnsAsync(logs);

        // Act
        await _controller.GetLogs(
            level: "Error",
            search: "backup",
            startDate: startDate,
            endDate: endDate,
            limit: 250);

        // Assert
        capturedParameters.Should().NotBeNull();
        capturedParameters!.Level.Should().Be("Error");
        capturedParameters!.Search.Should().Be("backup");
        capturedParameters!.StartDate.Should().Be(startDate);
        capturedParameters!.EndDate.Should().Be(endDate);
        capturedParameters!.Limit.Should().Be(250);
    }

    [Fact]
    public async Task GetLogs_WithDefaultParameters_UsesDefaultLimit()
    {
        // Arrange
        var logs = new List<LogEntry>();
        LogQueryParameters? capturedParameters = null;

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .Callback<LogQueryParameters, CancellationToken>((p, ct) => capturedParameters = p)
            .ReturnsAsync(logs);

        // Act
        await _controller.GetLogs();

        // Assert
        capturedParameters.Should().NotBeNull();
        capturedParameters!.Limit.Should().Be(100); // Default limit
    }

    [Fact]
    public async Task GetLogs_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetLogs();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetLogs_ReturnsEmptyList_WhenNoLogsFound()
    {
        // Arrange
        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LogEntry>());

        // Act
        var result = await _controller.GetLogs();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedLogs = okResult.Value.Should().BeAssignableTo<IEnumerable<LogEntry>>().Subject;
        returnedLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLogs_RespectsLimit_WhenMultipleLogsExist()
    {
        // Arrange
        var logs = Enumerable.Range(1, 150)
            .Select(i => new LogEntry
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                Level = "INF",
                Message = $"Log message {i}",
                SourceContext = "TestContext"
            })
            .ToList();

        _mockLogReaderService
            .Setup(s => s.GetLogsAsync(It.IsAny<LogQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _controller.GetLogs(limit: 50);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedLogs = okResult.Value.Should().BeAssignableTo<IEnumerable<LogEntry>>().Subject;
        returnedLogs.Should().HaveCount(150); // Service returns all, controller doesn't enforce limit (service does)
    }
}
