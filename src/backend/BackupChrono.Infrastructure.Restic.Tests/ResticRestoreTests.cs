using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Restic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupChrono.Infrastructure.Restic.Tests;

/// <summary>
/// Tests for ResticService restore operations
/// </summary>
public class ResticRestoreTests : IClassFixture<ResticTestFixture>
{
    private readonly ResticTestFixture _fixture;
    private readonly ResticService _resticService;
    private readonly ResticClient _resticClient;

    public ResticRestoreTests(ResticTestFixture fixture)
    {
        _fixture = fixture;
        _resticClient = new ResticClient(
            "/usr/local/bin/restic",
            _fixture.RepositoryPath,
            _fixture.ResticPassword,
            NullLogger<ResticClient>.Instance);
        _resticService = new ResticService(_resticClient, NullLogger<ResticService>.Instance);
    }

    [Fact(Skip = "Requires Docker container - run manually")]
    public async Task ListBackups_ReturnsEmptyList_WhenNoBackupsExist()
    {
        // Arrange - repository is initialized but has no backups yet

        // Act
        var backups = await _resticService.ListBackups();

        // Assert
        backups.Should().BeEmpty();
    }

    [Fact(Skip = "Requires Docker container - run manually")]
    public async Task GetBackup_ThrowsKeyNotFoundException_WhenBackupDoesNotExist()
    {
        // Arrange
        var nonExistentBackupId = "nonexistent";

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await _resticService.GetBackup(nonExistentBackupId);
        });
    }
    [Fact(Skip = "Requires Docker container - run manually")]
    public async Task BrowseBackup_ThrowsException_WhenBackupDoesNotExist()
    {
        // Arrange
        var nonExistentBackupId = "nonexistent";

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _resticService.BrowseBackup(nonExistentBackupId);
        });
    }
}
