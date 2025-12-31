namespace BackupChrono.Core.Entities;

/// <summary>
/// Supported backup protocol types for connecting to devices.
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// Server Message Block (SMB) - Windows file sharing protocol.
    /// Default port: 445
    /// </summary>
    SMB,

    /// <summary>
    /// Secure Shell (SSH) with SFTP - Unix/Linux file transfer protocol.
    /// Default port: 22
    /// </summary>
    SSH,

    /// <summary>
    /// Rsync protocol - Efficient incremental file synchronization.
    /// Default port: 873
    /// </summary>
    Rsync
}
