using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupChrono.Infrastructure.Restic.Tests;

public class ResticServiceTests : IClassFixture<ResticTestFixture>
{
    private readonly ResticTestFixture _fixture;
    private readonly ResticService _service;

    public ResticServiceTests(ResticTestFixture fixture)
    {
        _fixture = fixture;
        var client = new TestContainerResticClient(fixture.Container!, fixture.RepositoryPath, fixture.ResticPassword);
        _service = new ResticService(client, NullLogger<ResticService>.Instance);
    }

    [Fact]
    public async Task InitializeRepository_ShouldReturnTrue_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var newRepoPath = "/tmp/new-repo-" + Guid.NewGuid();
        
        // Act
        var result = await _service.InitializeRepository(newRepoPath, _fixture.ResticPassword);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeRepository_ShouldReturnFalse_WhenRepositoryAlreadyExists()
    {
        // Arrange - use the fixture's pre-initialized repo
        // Act
        var result = await _service.InitializeRepository(_fixture.RepositoryPath, _fixture.ResticPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RepositoryExists_ShouldReturnTrue_ForExistingRepo()
    {
        // Act
        var exists = await _service.RepositoryExists(_fixture.RepositoryPath);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RepositoryExists_ShouldReturnFalse_ForNonExistentRepo()
    {
        // Act
        var exists = await _service.RepositoryExists("/tmp/nonexistent-" + Guid.NewGuid());

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateBackup_ShouldCreateSnapshot_WhenSourceIsValid()
    {
        // Arrange
        var sourcePath = "/tmp/backup-test-" + Guid.NewGuid();
        await _fixture.Container!.ExecAsync(new[] { "/bin/sh", "-c", 
            $"mkdir -p {sourcePath} && echo 'content' > {sourcePath}/file.txt" });

        var device = new Device 
        { 
            Id = Guid.NewGuid(), 
            Name = "test-device",
            Protocol = ProtocolType.SSH,
            Host = "localhost",
            Username = "test",
            Password = new EncryptedCredential("test-password")
        };
        var rules = new IncludeExcludeRules();

        // Act
        var backup = await _service.CreateBackup(_fixture.RepositoryPath, device, null, sourcePath, rules);

        // Assert
        backup.Should().NotBeNull();
        backup.Status.Should().Be(BackupStatus.Success);
        backup.FilesNew.Should().BeGreaterThan(0);
    }
}
