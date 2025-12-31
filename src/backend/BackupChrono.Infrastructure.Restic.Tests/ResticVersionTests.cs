using BackupChrono.Infrastructure.Restic;
using Xunit;
using FluentAssertions;

namespace BackupChrono.Infrastructure.Restic.Tests;

/// <summary>
/// Tests for verifying restic binary version compatibility.
/// </summary>
public class ResticVersionTests : IClassFixture<ResticTestFixture>
{
    private readonly ResticTestFixture _fixture;

    public ResticVersionTests(ResticTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ResticClient_ShouldExecuteVersionCommand_WhenResticIsInstalled()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "restic", "version" });

        // Assert
        result.Stdout.Should().Contain("restic");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ResticClient_ShouldReturnVersionInfo_WithExpectedFormat()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "restic", "version" });

        // Assert
        result.Stdout.Should().MatchRegex(@"restic \d+\.\d+\.\d+");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ResticClient_ShouldRequireMinimumVersion_ForCompatibility()
    {
        // Arrange & Act
        var result = await _fixture.Container!.ExecAsync(new[] { "restic", "version" });
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
        var result = await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c",
            $"export RESTIC_PASSWORD={_fixture.ResticPassword} && restic snapshots --repo {_fixture.RepositoryPath} --json" });

        // Assert
        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("[]"); // Empty repository should return empty JSON array
    }
}
