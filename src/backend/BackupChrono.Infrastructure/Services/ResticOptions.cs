namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Configuration options for Restic backup service.
/// </summary>
public class ResticOptions
{
    /// <summary>
    /// Base path for storing restic repositories.
    /// Repositories are organized as {RepositoryBasePath}/{deviceId}/{shareId}.
    /// </summary>
    public string RepositoryBasePath { get; set; } = "./repositories";

    /// <summary>
    /// Path to the restic binary executable.
    /// </summary>
    public string BinaryPath { get; set; } = "restic";

    /// <summary>
    /// Password for restic repository encryption.
    /// </summary>
    public string? Password { get; set; }
}
