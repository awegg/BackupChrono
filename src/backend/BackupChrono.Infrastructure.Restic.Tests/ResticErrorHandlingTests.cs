using BackupChrono.Infrastructure.Restic;
using Xunit;
using FluentAssertions;

namespace BackupChrono.Infrastructure.Restic.Tests;

/// <summary>
/// Tests for verifying restic error handling scenarios.
/// </summary>
public class ResticErrorHandlingTests : IClassFixture<ResticTestFixture>
{
    private readonly ResticTestFixture _fixture;

    public ResticErrorHandlingTests(ResticTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Restic_ShouldReturnError_WhenBinaryNotFound()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "nonexistent-restic-binary", "version" });

        // Assert
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task Restic_ShouldReturnError_ForInvalidRepository()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic snapshots --repo /invalid/repo/path" });

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.Stderr.Should().Contain("Fatal");
    }

    [Fact]
    public async Task Restic_ShouldReturnError_ForIncorrectPassword()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD=wrong-password && restic check --repo {_fixture.RepositoryPath}" });

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.Stderr.Should().Contain("wrong password");
    }

    [Fact]
    public async Task Restic_ShouldBackup_WithValidInputs()
    {
        // Arrange - Create a test file
        await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c", 
            "mkdir -p /tmp/backup-source && echo 'test content' > /tmp/backup-source/test.txt" });

        // Act
        var result = await _fixture.Container.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic backup /tmp/backup-source --repo {_fixture.RepositoryPath} --json" });

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("files_new");
    }

    [Fact]
    public async Task Restic_ShouldComplete_WithinReasonableTime()
    {
        // Arrange - Create cancellation token with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _fixture.Container!.ExecAsync(new[] { "restic", "version" }, cts.Token);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Restic_ShouldCheck_RepositoryIntegrity()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic check --repo {_fixture.RepositoryPath}" });

        // Assert
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Restic_ShouldHandleLockContention_Gracefully()
    {
        // Arrange - Create multiple snapshots concurrently
        await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c", 
            "mkdir -p /tmp/source1 /tmp/source2 /tmp/source3 && " +
            "echo 'data1' > /tmp/source1/file.txt && " +
            "echo 'data2' > /tmp/source2/file.txt && " +
            "echo 'data3' > /tmp/source3/file.txt" });

        // Act
        var tasks = new[]
        {
            _fixture.Container.ExecAsync(new[] { "/bin/sh", "-c", $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic backup /tmp/source1 --repo {_fixture.RepositoryPath}" }),
            _fixture.Container.ExecAsync(new[] { "/bin/sh", "-c", $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic backup /tmp/source2 --repo {_fixture.RepositoryPath}" }),
            _fixture.Container.ExecAsync(new[] { "/bin/sh", "-c", $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic backup /tmp/source3 --repo {_fixture.RepositoryPath}" })
        };

        // Assert - At least one succeeds, others may fail due to locking
        var results = await Task.WhenAll(tasks);
        results.Should().Contain(r => r.ExitCode == 0, "at least one backup should succeed");
    }
}
