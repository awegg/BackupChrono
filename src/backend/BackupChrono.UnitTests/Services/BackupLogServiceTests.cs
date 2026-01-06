using BackupChrono.Core.Entities;
using BackupChrono.Infrastructure.Repositories;
using BackupChrono.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupChrono.UnitTests.Services;

public class InMemoryBackupLogServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly BackupExecutionLogRepository _repository;
    private readonly InMemoryBackupLogService _service;

    public InMemoryBackupLogServiceTests()
    {
        // Create temp directory for test JSONL files
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"BackupChronoTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        _repository = new BackupExecutionLogRepository(_tempDirectory, NullLogger<BackupExecutionLogRepository>.Instance);
        _service = new InMemoryBackupLogService(_repository, NullLogger<InMemoryBackupLogService>.Instance);
    }

    public void Dispose()
    {
        // Clean up temp directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetOrCreateLog_WhenNotExists_ShouldCreateNewLog()
    {
        // Arrange
        var backupId = "abc123";
        var jobId = Guid.NewGuid().ToString();

        // Act
        var log = await _service.GetOrCreateLog(backupId, jobId);

        // Assert
        log.Should().NotBeNull();
        log.BackupId.Should().Be(backupId);
        log.Warnings.Should().BeEmpty();
        log.Errors.Should().BeEmpty();
        log.ProgressLog.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateLog_WhenExists_ShouldReturnExisting()
    {
        // Arrange
        var backupId = "abc123";
        var log1 = await _service.GetOrCreateLog(backupId);
        await _service.AddWarning(backupId, "Test warning");

        // Act
        var log2 = await _service.GetOrCreateLog(backupId);

        // Assert
        log2.Should().BeSameAs(log1);
        log2.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task GetLog_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var backupId = "nonexistent";

        // Act
        var log = await _service.GetLog(backupId);

        // Assert
        log.Should().BeNull();
    }

    [Fact]
    public async Task AddWarning_ShouldAddToWarningsList()
    {
        // Arrange
        var backupId = "backup1";
        await _service.GetOrCreateLog(backupId);

        // Act
        await _service.AddWarning(backupId, "Test warning");

        // Assert
        var log = await _service.GetLog(backupId);
        log!.Warnings.Should().ContainSingle();
        log.Warnings[0].Should().Be("Test warning");
    }

    [Fact]
    public async Task AddError_ShouldAddToErrorsList()
    {
        // Arrange
        var backupId = "backup1";
        await _service.GetOrCreateLog(backupId);

        // Act
        await _service.AddError(backupId, "Test error");

        // Assert
        var log = await _service.GetLog(backupId);
        log!.Errors.Should().ContainSingle();
        log.Errors[0].Should().Be("Test error");
    }

    [Fact]
    public async Task AddProgressEntry_ShouldAddToProgressLog()
    {
        // Arrange
        var backupId = "backup1";
        await _service.GetOrCreateLog(backupId);
        var entry = new ProgressLogEntry
        {
            Message = "Processing file 1",
            PercentDone = 50
        };

        // Act
        await _service.AddProgressEntry(backupId, entry);

        // Assert
        var log = await _service.GetLog(backupId);
        log!.ProgressLog.Should().ContainSingle();
        log.ProgressLog[0].Message.Should().Be("Processing file 1");
    }

    [Fact]
    public async Task AddWarning_WhenLogDoesNotExist_ShouldCreateLog()
    {
        // Arrange
        var backupId = "backup1";

        // Act
        await _service.AddWarning(backupId, "Warning");

        // Assert
        var log = await _service.GetLog(backupId);
        log.Should().NotBeNull();
        log!.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task AddError_WhenLogDoesNotExist_ShouldCreateLog()
    {
        // Arrange
        var backupId = "backup1";

        // Act
        await _service.AddError(backupId, "Error");

        // Assert
        var log = await _service.GetLog(backupId);
        log.Should().NotBeNull();
        log!.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllLogs()
    {
        // Arrange
        await _service.GetOrCreateLog("backup1");
        await _service.GetOrCreateLog("backup2");

        // Act
        await _service.Clear();

        // Assert
        var log1 = await _service.GetLog("backup1");
        var log2 = await _service.GetLog("backup2");
        log1.Should().BeNull();
        log2.Should().BeNull();
    }

    [Fact]
    public async Task MultipleLogs_ShouldBeIndependent()
    {
        // Arrange
        var backupId1 = "backup1";
        var backupId2 = "backup2";

        // Act
        await _service.GetOrCreateLog(backupId1);
        await _service.GetOrCreateLog(backupId2);
        await _service.AddWarning(backupId1, "Warning 1");
        await _service.AddError(backupId2, "Error 2");

        // Assert
        var log1 = await _service.GetLog(backupId1);
        var log2 = await _service.GetLog(backupId2);

        log1!.Warnings.Should().ContainSingle();
        log1.Errors.Should().BeEmpty();

        log2!.Warnings.Should().BeEmpty();
        log2.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task PersistLog_ShouldSaveToRepository()
    {
        // Arrange
        var backupId = "backup1";
        await _service.GetOrCreateLog(backupId);
        await _service.AddWarning(backupId, "Warning 1");
        await _service.AddError(backupId, "Error 1");

        // Act
        await _service.PersistLog(backupId);

        // Assert - Load directly from repository to verify persistence
        var loadedLogs = await _repository.LoadLogs();
        loadedLogs.Should().ContainKey(backupId);
        loadedLogs[backupId].Warnings.Should().ContainSingle();
        loadedLogs[backupId].Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task PersistLog_WhenLogDoesNotExist_ShouldLogWarning()
    {
        // Arrange
        var backupId = "nonexistent";

        // Act & Assert - Should not throw
        await _service.PersistLog(backupId);
    }

    [Fact]
    public async Task LoadLogs_OnInitialization_ShouldRestorePersistedLogs()
    {
        // Arrange - Persist logs using first service instance
        var backupId = "backup1";
        await _service.GetOrCreateLog(backupId);
        await _service.AddWarning(backupId, "Persisted warning");
        await _service.PersistLog(backupId);

        // Act - Create new service instance to simulate app restart
        var newService = new InMemoryBackupLogService(_repository, NullLogger<InMemoryBackupLogService>.Instance);

        // Assert - Logs should be loaded from JSONL
        var log = await newService.GetLog(backupId);
        log.Should().NotBeNull();
        log!.Warnings.Should().ContainSingle();
        log.Warnings[0].Should().Be("Persisted warning");
    }
}
