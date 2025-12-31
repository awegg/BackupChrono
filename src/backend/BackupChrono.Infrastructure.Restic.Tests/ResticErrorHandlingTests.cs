using BackupChrono.Infrastructure.Restic;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;
using FluentAssertions;

namespace BackupChrono.Infrastructure.Restic.Tests;

/// <summary>
/// Tests for verifying restic error handling scenarios.
/// </summary>
public class ResticErrorHandlingTests : IAsyncLifetime
{
    private IContainer? _container;
    private string _repositoryPath = string.Empty;
    private const string ResticPassword = "test-password-123";

    public async Task InitializeAsync()
    {
        // Build a container with restic installed
        _container = new ContainerBuilder()
            .WithImage("alpine:latest")
            .WithCommand("/bin/sh", "-c", "while true; do sleep 30; done")
            .Build();

        await _container.StartAsync();

        // Install restic in the container
        var installResult = await _container.ExecAsync(new[] { "/bin/sh", "-c", 
            "apk add --no-cache wget ca-certificates bzip2 && " +
            "wget -q https://github.com/restic/restic/releases/download/v0.17.3/restic_0.17.3_linux_amd64.bz2 && " +
            "bunzip2 restic_0.17.3_linux_amd64.bz2 && " +
            "mv restic_0.17.3_linux_amd64 /usr/local/bin/restic && " +
            "chmod +x /usr/local/bin/restic" });

        if (installResult.ExitCode != 0)
        {
            throw new Exception($"Failed to install restic: {installResult.Stderr}");
        }

        // Initialize a test repository
        _repositoryPath = "/tmp/test-repo";
        await _container.ExecAsync(new[] { "/bin/sh", "-c", $"mkdir -p {_repositoryPath}" });
        
        var initResult = await _container.ExecAsync(new[] { "/bin/sh", "-c", 
            $"export RESTIC_PASSWORD={ResticPassword} && restic init --repo {_repositoryPath}" });
        
        if (initResult.ExitCode != 0)
        {
            throw new Exception($"Failed to initialize restic repository: {initResult.Stderr}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Restic_ShouldReturnError_WhenBinaryNotFound()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "nonexistent-restic-binary", "version" });

        // Assert
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task Restic_ShouldReturnError_ForInvalidRepository()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={ResticPassword} && restic snapshots --repo /invalid/repo/path" });

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.Stderr.Should().Contain("Fatal");
    }

    [Fact]
    public async Task Restic_ShouldReturnError_ForIncorrectPassword()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD=wrong-password && restic check --repo {_repositoryPath}" });

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.Stderr.Should().Contain("wrong password");
    }

    [Fact]
    public async Task Restic_ShouldBackup_WithValidInputs()
    {
        // Arrange - Create a test file
        await _container!.ExecAsync(new[] { "/bin/sh", "-c", 
            "mkdir -p /tmp/backup-source && echo 'test content' > /tmp/backup-source/test.txt" });

        // Act
        var result = await _container.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={ResticPassword} && restic backup /tmp/backup-source --repo {_repositoryPath} --json" });

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
        await _container!.ExecAsync(new[] { "restic", "version" }, cts.Token);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Restic_ShouldCheck_RepositoryIntegrity()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={ResticPassword} && restic check --repo {_repositoryPath}" });

        // Assert
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Restic_ShouldHandleLockContention_Gracefully()
    {
        // Arrange - Create multiple snapshots concurrently
        await _container!.ExecAsync(new[] { "/bin/sh", "-c", 
            "mkdir -p /tmp/source1 /tmp/source2 /tmp/source3 && " +
            "echo 'data1' > /tmp/source1/file.txt && " +
            "echo 'data2' > /tmp/source2/file.txt && " +
            "echo 'data3' > /tmp/source3/file.txt" });

        // Act
        var tasks = new[]
        {
            _container.ExecAsync(new[] { "/bin/sh", "-c", $"export RESTIC_PASSWORD={ResticPassword} && restic backup /tmp/source1 --repo {_repositoryPath}" }),
            _container.ExecAsync(new[] { "/bin/sh", "-c", $"export RESTIC_PASSWORD={ResticPassword} && restic backup /tmp/source2 --repo {_repositoryPath}" }),
            _container.ExecAsync(new[] { "/bin/sh", "-c", $"export RESTIC_PASSWORD={ResticPassword} && restic backup /tmp/source3 --repo {_repositoryPath}" })
        };

        // Assert - At least one succeeds, others may fail due to locking
        var results = await Task.WhenAll(tasks);
        results.Should().Contain(r => r.ExitCode == 0, "at least one backup should succeed");
    }
}
