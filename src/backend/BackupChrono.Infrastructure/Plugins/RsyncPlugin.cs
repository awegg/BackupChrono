using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// Rsync protocol plugin for Unix/Linux servers.
/// TODO: Implement by spawning rsync process for synchronization.
/// </summary>
public class RsyncPlugin : IProtocolPlugin
{
    public string ProtocolName => "Rsync";

    public bool SupportsWakeOnLan => true;

    public bool RequiresAuthentication => true;
    public Task<bool> TestConnection(Device device)
    {
        throw new NotImplementedException("Rsync connection test not yet implemented. Requires rsync process integration.");
    }

    public Task<string> MountShare(Device device, Share share)
    {
        throw new NotImplementedException("Rsync connection preparation not yet implemented. Requires rsync process integration.");
    }

    public Task UnmountShare(string mountPath)
    {
        // Rsync doesn't maintain persistent connections, but throw for consistency
        throw new NotImplementedException("Rsync doesn't maintain persistent connections.");
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
