using BackupChrono.Core.Interfaces;
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
        try
        {
            var resticClient = new ResticClient("restic", tempPath, "password");
            var monitor = new StorageMonitor(new NullLogger<StorageMonitor>(), resticClient);

            // Act
            var result = await monitor.GetAllRepositoryStorageStatus();

            // Assert
            Assert.Empty(result);
        }
        finally
        {
            // Cleanup in case directory was created
            if (Directory.Exists(tempPath))
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
            var status = result[0];
            Assert.Equal(tempPath, status.Path);
            Assert.True(status.TotalBytes >= 0, "TotalBytes should be non-negative");
            Assert.True(status.AvailableBytes >= 0, "AvailableBytes should be non-negative");
            Assert.True(status.UsedBytes >= 0, "UsedBytes should be non-negative");
            // ThresholdLevel is a value type (enum), so it's always non-null
            Assert.True(Enum.IsDefined(typeof(StorageThresholdLevel), status.ThresholdLevel));
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
