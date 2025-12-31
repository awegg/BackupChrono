using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// SSH/SFTP protocol plugin for remote Unix/Linux servers.
/// TODO: Implement using SSH.NET library for SFTP access.
/// </summary>
public class SshPlugin : IProtocolPlugin
{
    public string ProtocolName => "SSH";

    public bool SupportsWakeOnLan => true;

    public bool RequiresAuthentication => true;

    public Task<bool> TestConnection(Device device)
    {
        throw new NotImplementedException("SSH connection test not yet implemented. Requires SSH.NET integration.");
    }

    public Task<string> MountShare(Device device, Share share)
    {
        throw new NotImplementedException("SFTP connection not yet implemented. Requires SSH.NET integration.");
    }

    public Task UnmountShare(string mountPath)
    {
        throw new NotImplementedException("SFTP disconnection not yet implemented. Requires SSH.NET integration.");
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
