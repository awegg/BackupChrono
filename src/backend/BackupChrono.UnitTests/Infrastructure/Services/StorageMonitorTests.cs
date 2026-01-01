using BackupChrono.Infrastructure.Restic;
using BackupChrono.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupChrono.UnitTests.Infrastructure.Services;

public class StorageMonitorTests
{
    [Fact]
    public async Task GetAllRepositoryStorageStatus_ReturnsEmpty_WhenRepositoryDirectoryMissing()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var resticClient = new ResticClient("restic", tempPath, "password");
        var monitor = new StorageMonitor(new NullLogger<StorageMonitor>(), resticClient);

        // Act
        var result = await monitor.GetAllRepositoryStorageStatus();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllRepositoryStorageStatus_ReturnsStatus_WhenRepositoryDirectoryExists()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);
        try
        {
            var resticClient = new ResticClient("restic", tempPath, "password");
            var monitor = new StorageMonitor(new NullLogger<StorageMonitor>(), resticClient);

            // Act
            var result = (await monitor.GetAllRepositoryStorageStatus()).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(tempPath, result[0].Path);
        }
        finally
        {
            try
            {
                Directory.Delete(tempPath, recursive: true);
            }
            catch
            {
                // ignore cleanup failures in tests
            }
        }
    }
}
