using BackupChrono.Core.Entities;
using FluentAssertions;
using Xunit;

namespace BackupChrono.UnitTests.Entities;

public class BackupExecutionLogTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var log = new BackupExecutionLog
        {
            BackupId = "abc123"
        };

        // Assert
        log.BackupId.Should().Be("abc123");
        log.Id.Should().NotBeNullOrEmpty();
        log.Warnings.Should().NotBeNull();
        log.Warnings.Should().BeEmpty();
        log.Errors.Should().NotBeNull();
        log.Errors.Should().BeEmpty();
        log.ProgressLog.Should().NotBeNull();
        log.ProgressLog.Should().BeEmpty();
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        log.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Warnings_ShouldBeModifiable()
    {
        // Arrange
        var log = new BackupExecutionLog { BackupId = "backup1" };

        // Act
        log.Warnings.Add("Warning 1");
        log.Warnings.Add("Warning 2");

        // Assert
        log.Warnings.Should().HaveCount(2);
        log.Warnings.Should().Contain("Warning 1");
        log.Warnings.Should().Contain("Warning 2");
    }

    [Fact]
    public void Errors_ShouldBeModifiable()
    {
        // Arrange
        var log = new BackupExecutionLog { BackupId = "backup1" };

        // Act
        log.Errors.Add("Error 1");
        log.Errors.Add("Error 2");

        // Assert
        log.Errors.Should().HaveCount(2);
        log.Errors.Should().Contain("Error 1");
        log.Errors.Should().Contain("Error 2");
    }

    [Fact]
    public void ProgressLog_ShouldBeModifiable()
    {
        // Arrange
        var log = new BackupExecutionLog { BackupId = "backup1" };

        // Act
        log.ProgressLog.Add(new ProgressLogEntry { Message = "Step 1" });
        log.ProgressLog.Add(new ProgressLogEntry { Message = "Step 2" });
        log.ProgressLog.Add(new ProgressLogEntry { Message = "Step 3" });

        // Assert
        log.ProgressLog.Should().HaveCount(3);
        log.ProgressLog.Select(p => p.Message).Should().ContainInOrder("Step 1", "Step 2", "Step 3");
    }

    [Fact]
    public void JobId_ShouldBeOptional()
    {
        // Arrange & Act
        var log = new BackupExecutionLog { BackupId = "backup1" };

        // Assert
        log.JobId.Should().BeNull();
    }
}

public class ProgressLogEntryTests
{
    [Fact]
    public void Constructor_ShouldInitializeTimestamp()
    {
        // Act
        var entry = new ProgressLogEntry { Message = "Test" };

        // Assert
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        entry.Message.Should().Be("Test");
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Act
        var entry = new ProgressLogEntry
        {
            Message = "Processing",
            PercentDone = 50,
            CurrentFile = "/path/to/file.txt",
            FilesDone = 100,
            BytesDone = 1024000
        };

        // Assert
        entry.Message.Should().Be("Processing");
        entry.PercentDone.Should().Be(50);
        entry.CurrentFile.Should().Be("/path/to/file.txt");
        entry.FilesDone.Should().Be(100);
        entry.BytesDone.Should().Be(1024000);
    }
}
