using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// SMB/CIFS protocol plugin for Windows network shares.
/// TODO: Implement using SMBLibrary for cross-platform SMB access.
/// </summary>
public class SmbPlugin : IProtocolPlugin
{
    public string ProtocolName => "SMB";

    public bool SupportsWakeOnLan => true;

    public bool RequiresAuthentication => true;

    public Task<bool> TestConnection(Device device)
    {
        throw new NotImplementedException("SMB connection test not yet implemented. Requires SMBLibrary integration.");
    }

    public Task<string> MountShare(Device device, Share share)
    {
        throw new NotImplementedException("SMB share mounting not yet implemented. Requires SMBLibrary integration.");
    }

    public Task UnmountShare(string mountPath)
    {
        throw new NotImplementedException("SMB share unmounting not yet implemented. Requires SMBLibrary integration.");
    }

    public async Task WakeDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.WakeOnLanMacAddress))
        {
            throw new InvalidOperationException("Device does not have Wake-on-LAN MAC address configured");
        }

        await WakeOnLanHelper.SendMagicPacket(device.WakeOnLanMacAddress);
    }
}
