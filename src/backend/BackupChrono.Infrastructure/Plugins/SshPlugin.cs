using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;

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

    public async Task<bool> TestConnection(Device device)
    {
        // TODO: Implement SSH connection test using SSH.NET
        await Task.CompletedTask;
        return false;
    }

    public async Task<string> MountShare(Device device, Share share)
    {
        // TODO: Implement SFTP connection using SSH.NET
        await Task.CompletedTask;
        var port = device.Port ?? 22;
        return $"sftp:{device.Username}@{device.Host}:{port}{share.Path}";
    }

    public async Task UnmountShare(string mountPath)
    {
        // TODO: Implement SFTP disconnection
        await Task.CompletedTask;
    }

    public async Task WakeDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.WakeOnLanMacAddress))
        {
            throw new InvalidOperationException("Device does not have Wake-on-LAN MAC address configured");
        }

        var macBytes = ParseMacAddress(device.WakeOnLanMacAddress);
        var magicPacket = BuildWolPacket(macBytes);

        using var udpClient = new System.Net.Sockets.UdpClient();
        udpClient.EnableBroadcast = true;
        
        await udpClient.SendAsync(magicPacket, magicPacket.Length, new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 9));
    }

    private static byte[] ParseMacAddress(string macAddress)
    {
        var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(".", "");
        if (cleanMac.Length != 12)
            throw new ArgumentException("Invalid MAC address format");

        return Enumerable.Range(0, 6)
            .Select(i => Convert.ToByte(cleanMac.Substring(i * 2, 2), 16))
            .ToArray();
    }

    private static byte[] BuildWolPacket(byte[] macBytes)
    {
        var packet = new byte[102];
        
        // First 6 bytes are 0xFF
        for (int i = 0; i < 6; i++)
            packet[i] = 0xFF;

        // Repeat MAC address 16 times
        for (int i = 0; i < 16; i++)
        {
            Array.Copy(macBytes, 0, packet, 6 + i * 6, 6);
        }

        return packet;
    }
}
