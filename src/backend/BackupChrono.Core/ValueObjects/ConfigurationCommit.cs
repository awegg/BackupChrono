namespace BackupChrono.Core.ValueObjects;

/// <summary>
/// Represents a version-controlled change to system configuration stored in Git.
/// </summary>
public class ConfigurationCommit
{
    /// <summary>
    /// Git commit SHA hash.
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Commit timestamp.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Commit author name.
    /// </summary>
    public required string Author { get; init; }

    /// <summary>
    /// Commit author email.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Commit message describing the change.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// List of file paths that were changed in this commit.
    /// </summary>
    public string[] FilesChanged { get; init; } = Array.Empty<string>();
}
