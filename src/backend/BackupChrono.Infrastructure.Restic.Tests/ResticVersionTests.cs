using BackupChrono.Infrastructure.Restic;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;
using FluentAssertions;

namespace BackupChrono.Infrastructure.Restic.Tests;

/// <summary>
/// Tests for verifying restic binary version compatibility.
/// </summary>
public class ResticVersionTests : IAsyncLifetime
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
    public async Task ResticClient_ShouldExecuteVersionCommand_WhenResticIsInstalled()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "restic", "version" });

        // Assert
        result.Stdout.Should().Contain("restic");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ResticClient_ShouldReturnVersionInfo_WithExpectedFormat()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "restic", "version" });

        // Assert
        result.Stdout.Should().MatchRegex(@"restic \d+\.\d+\.\d+");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ResticClient_ShouldRequireMinimumVersion_ForCompatibility()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "restic", "version" });
        var versionMatch = System.Text.RegularExpressions.Regex.Match(result.Stdout, @"restic (\d+)\.(\d+)\.(\d+)");
        
        // Assert
        versionMatch.Success.Should().BeTrue();
        var version = new Version(
            int.Parse(versionMatch.Groups[1].Value),
            int.Parse(versionMatch.Groups[2].Value),
            int.Parse(versionMatch.Groups[3].Value)
        );
        
        var minimumVersion = new Version(0, 16, 0);
        version.Should().BeGreaterThanOrEqualTo(minimumVersion);
    }

    [Fact]
    public async Task Restic_ShouldListSnapshots_FromInitializedRepository()
    {
        // Arrange & Act
        var result = await _container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={ResticPassword} && restic snapshots --repo {_repositoryPath} --json" });

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("[]"); // Empty repository should return empty JSON array
    }
}
