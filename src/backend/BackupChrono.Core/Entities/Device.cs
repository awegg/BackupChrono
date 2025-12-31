using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Entities;

/// <summary>
/// Represents a physical or virtual machine being backed up (NAS, server, workstation).
/// </summary>
public class Device
{
    /// <summary>
    /// Unique identifier for the device.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the device (e.g., "office-nas", "web-server-1").
    /// Must be DNS-compatible (alphanumeric, hyphens, no spaces).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Protocol type for connecting to the device (SMB, SSH, Rsync).
    /// </summary>
    public required ProtocolType Protocol { get; set; }

    /// <summary>
    /// IP address or hostname of the device.
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// Optional port override. If null, uses protocol default (SMB=445, SSH=22, Rsync=873).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Authentication username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Authentication password/credential (stored encrypted in Git configuration).
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Whether Wake-on-LAN is enabled for this device.
    /// </summary>
    public bool WakeOnLanEnabled { get; set; }

    /// <summary>
    /// MAC address for Wake-on-LAN (required if WakeOnLanEnabled is true).
    /// </summary>
    public string? WakeOnLanMacAddress { get; set; }

    /// <summary>
    /// Device-level backup schedule. If null, inherits from global configuration.
    /// </summary>
    public Schedule? Schedule { get; set; }

    /// <summary>
    /// Device-level retention policy. If null, inherits from global configuration.
    /// </summary>
    public RetentionPolicy? RetentionPolicy { get; set; }

    /// <summary>
    /// Device-level include/exclude rules. If null, inherits from global configuration.
    /// </summary>
    public IncludeExcludeRules? IncludeExcludeRules { get; set; }

    /// <summary>
    /// Timestamp when the device was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the device was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
