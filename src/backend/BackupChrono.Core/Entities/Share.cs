using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Entities;

/// <summary>
/// Represents a specific path or mount point on a device to be backed up.
/// </summary>
public class Share
{
    /// <summary>
    /// Unique identifier for the share.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the parent device.
    /// </summary>
    public required Guid DeviceId { get; set; }

    /// <summary>
    /// Display name for the share (e.g., "shared", "backup", "var-www").
    /// Must be unique within a device.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Share path (e.g., "/data", "\\share", "C:\Users").
    /// Format validated based on device protocol (UNC for SMB, Unix paths for SSH/Rsync).
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Whether this share is actively backed up.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Share-level schedule override. If null, inherits from device.
    /// </summary>
    public Schedule? Schedule { get; set; }

    /// <summary>
    /// Share-level retention policy override. If null, inherits from device.
    /// </summary>
    public RetentionPolicy? RetentionPolicy { get; set; }

    /// <summary>
    /// Share-level include/exclude rules override. If null, inherits from device.
    /// If defined (even as empty array), completely replaces device+global patterns.
    /// </summary>
    public IncludeExcludeRules? IncludeExcludeRules { get; set; }

    /// <summary>
    /// Timestamp when the share was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the share was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
