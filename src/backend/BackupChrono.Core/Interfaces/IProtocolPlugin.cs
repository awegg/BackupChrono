namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Plugin interface for extensible source protocols (SMB, SSH, Rsync, NFS, etc.).
/// </summary>
public interface IProtocolPlugin
{
    /// <summary>
    /// Protocol name identifier (e.g., "SMB", "SSH", "Rsync", "NFS").
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Whether this protocol supports Wake-on-LAN functionality.
    /// </summary>
    bool SupportsWakeOnLan { get; }

    /// <summary>
    /// Whether this protocol requires authentication.
    /// </summary>
    bool RequiresAuthentication { get; }

    /// <summary>
    /// Tests connectivity to the device.
    /// </summary>
    /// <param name="device">Device to test connection to.</param>
    /// <returns>True if connection successful, false otherwise.</returns>
    Task<bool> TestConnection(Entities.Device device);

    /// <summary>
    /// Mounts a share and returns the local path for restic to backup.
    /// </summary>
    /// <param name="device">Device containing the share.</param>
    /// <param name="share">Share to mount.</param>
    /// <returns>Local mount path accessible by restic.</returns>
    Task<string> MountShare(Entities.Device device, Entities.Share share);

    /// <summary>
    /// Unmounts a previously mounted share.
    /// </summary>
    /// <param name="mountPath">Local mount path to unmount.</param>
    Task UnmountShare(string mountPath);

    /// <summary>
    /// Sends Wake-on-LAN magic packet to wake the device.
    /// </summary>
    /// <param name="device">Device to wake (must have WakeOnLanMacAddress configured).</param>
    Task WakeDevice(Entities.Device device);
}
