using BackupChrono.Core.DTOs;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Services;

/// <summary>
/// Unit tests for LogReaderService
/// </summary>
public class LogReaderServiceTests : IDisposable
{
    private readonly string _testLogDirectory;
    private readonly Mock<ILogger<LogReaderService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public LogReaderServiceTests()
    {
        // Create temporary test directory
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"BackupChronoTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDirectory);

        _mockLogger = new Mock<ILogger<LogReaderService>>();

        // Setup configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Serilog:WriteTo:1:Args:path", Path.Combine(_testLogDirectory, "backupchrono-.log") }
        });
        _configuration = configBuilder.Build();
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testLogDirectory))
        {
            Directory.Delete(_testLogDirectory, true);
        }
    }

    [Fact]
    public async Task GetLogsAsync_ReturnsEmptyList_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Serilog:WriteTo:1:Args:path", "nonexistent/backupchrono-.log" }
            })
            .Build();

        var service = new LogReaderService(nonExistentConfig, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLogsAsync_ParsesValidLogLines_Correctly()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 21:10:52.832 +01:00 [INF] Application started
2026-01-11 21:10:53.123 +01:00 [WRN] Warning message here
2026-01-11 21:10:54.456 +01:00 [ERR] Error occurred
2026-01-11 21:10:55.789 +01:00 [DBG] Debug message";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(4);
        logs[0].Level.Should().Be("DBG");
        logs[0].Message.Should().Be("Debug message");
        logs[1].Level.Should().Be("ERR");
        logs[1].Message.Should().Be("Error occurred");
        logs[2].Level.Should().Be("WRN");
        logs[3].Level.Should().Be("INF");
    }

    [Fact]
    public async Task GetLogsAsync_FiltersBy_Level()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 21:10:52.832 +01:00 [INF] Info message 1
2026-01-11 21:10:53.123 +01:00 [WRN] Warning message
2026-01-11 21:10:54.456 +01:00 [ERR] Error message
2026-01-11 21:10:55.789 +01:00 [INF] Info message 2";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Level = "Error", Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(1);
        logs[0].Level.Should().Be("ERR");
        logs[0].Message.Should().Be("Error message");
    }

    [Fact]
    public async Task GetLogsAsync_FiltersBy_SearchText()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 21:10:52.832 +01:00 [INF] Backup job started
2026-01-11 21:10:53.123 +01:00 [INF] Processing files
2026-01-11 21:10:54.456 +01:00 [INF] Backup completed successfully
2026-01-11 21:10:55.789 +01:00 [INF] System idle";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Search = "backup", Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(2);
        logs.All(l => l.Message.Contains("Backup", StringComparison.OrdinalIgnoreCase) || 
                     l.Message.Contains("backup", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetLogsAsync_FiltersBy_DateRange()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 10:00:00.000 +01:00 [INF] Early message
2026-01-11 15:00:00.000 +01:00 [INF] Middle message
2026-01-11 20:00:00.000 +01:00 [INF] Late message";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var startDate = new DateTime(2026, 1, 11, 14, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 11, 19, 0, 0, DateTimeKind.Utc);
        var parameters = new LogQueryParameters 
        { 
            StartDate = startDate,
            EndDate = endDate,
            Limit = 100 
        };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(1);
        logs[0].Message.Should().Be("Middle message");
    }

    [Fact]
    public async Task GetLogsAsync_RespectsLimit()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logLines = Enumerable.Range(1, 100)
            .Select(i => $"2026-01-11 21:{i % 60:D2}:{i % 60:D2}.000 +01:00 [INF] Message {i}");
        var logContent = string.Join(Environment.NewLine, logLines);

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 10 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetLogsAsync_EnforcesMaxLimit_Of1000()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        // Create 1500 log entries
        var logLines = Enumerable.Range(1, 1500)
            .Select(i => $"2026-01-11 21:10:52.{i % 1000:D3} +01:00 [INF] Message {i}");
        var logContent = string.Join(Environment.NewLine, logLines);

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 2000 }; // Request more than max

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        result.Should().HaveCountLessThanOrEqualTo(1000);
    }

    [Fact]
    public async Task GetLogsAsync_ReturnsNewest_LogsFirst()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 21:10:52.000 +01:00 [INF] First message
2026-01-11 21:10:53.000 +01:00 [INF] Second message
2026-01-11 21:10:54.000 +01:00 [INF] Third message";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(3);
        logs[0].Message.Should().Be("Third message"); // Newest first
        logs[1].Message.Should().Be("Second message");
        logs[2].Message.Should().Be("First message");
    }

    [Fact]
    public async Task GetLogsAsync_HandlesMultilineMessages()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 21:10:52.832 +01:00 [ERR] Exception occurred
System.Exception: Test exception
   at SomeClass.SomeMethod()
2026-01-11 21:10:53.123 +01:00 [INF] Next message";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCountGreaterThan(0);
        // Should handle multiline gracefully (even if it doesn't perfectly parse the exception)
    }

    [Fact]
    public async Task GetLogsAsync_SkipsInvalidLogLines()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 21:10:52.832 +01:00 [INF] Valid message
This is an invalid line without proper format
2026-01-11 21:10:53.123 +01:00 [WRN] Another valid message";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(2); // Only valid lines
        logs[0].Message.Should().Be("Another valid message");
        logs[1].Message.Should().Be("Valid message");
    }

    [Fact]
    public async Task GetLogsAsync_HandlesEmptyFile()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        await File.WriteAllTextAsync(logFilePath, string.Empty);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLogsAsync_ReadsMultipleLogFiles_InOrder()
    {
        // Arrange
        var oldLogFile = Path.Combine(_testLogDirectory, "backupchrono-20260110.log");
        var newLogFile = Path.Combine(_testLogDirectory, "backupchrono-20260111.log");

        await File.WriteAllTextAsync(oldLogFile, 
            "2026-01-10 21:10:52.000 +01:00 [INF] Old log message");
        await File.WriteAllTextAsync(newLogFile, 
            "2026-01-11 21:10:52.000 +01:00 [INF] New log message");

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters { Limit = 100 };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(2);
        logs[0].Message.Should().Be("New log message"); // Newer file first
        logs[1].Message.Should().Be("Old log message");
    }

    [Fact]
    public async Task GetLogsAsync_CombinesAllFilters()
    {
        // Arrange
        var logFileName = $"backupchrono-{DateTime.Now:yyyyMMdd}.log";
        var logFilePath = Path.Combine(_testLogDirectory, logFileName);

        var logContent = @"2026-01-11 10:00:00.000 +01:00 [INF] Backup started
2026-01-11 15:00:00.000 +01:00 [ERR] Backup failed with error
2026-01-11 20:00:00.000 +01:00 [ERR] Database error occurred
2026-01-11 21:00:00.000 +01:00 [WRN] Backup completed with warnings";

        await File.WriteAllTextAsync(logFilePath, logContent);

        var service = new LogReaderService(_configuration, _mockLogger.Object);
        var parameters = new LogQueryParameters 
        { 
            Level = "Error",
            Search = "backup",
            StartDate = new DateTime(2026, 1, 11, 14, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 1, 11, 19, 0, 0, DateTimeKind.Utc),
            Limit = 100 
        };

        // Act
        var result = await service.GetLogsAsync(parameters);

        // Assert
        var logs = result.ToList();
        logs.Should().HaveCount(1);
        logs[0].Level.Should().Be("ERR");
        logs[0].Message.Should().Be("Backup failed with error");
    }
}
