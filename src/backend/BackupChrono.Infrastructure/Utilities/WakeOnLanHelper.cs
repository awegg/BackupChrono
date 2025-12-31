using System.Net;
using System.Net.Sockets;

namespace BackupChrono.Infrastructure.Utilities;

/// <summary>
/// Shared utility for Wake-on-LAN operations.
/// </summary>
public static class WakeOnLanHelper
{
    /// <summary>
    /// Sends a Wake-on-LAN magic packet to the specified MAC address.
    /// </summary>
    /// <param name="macAddress">MAC address in any standard format (e.g., "00:11:22:33:44:55", "00-11-22-33-44-55", "001122334455")</param>
    public static async Task SendMagicPacket(string macAddress)
    {
        var macBytes = ParseMacAddress(macAddress);
        var magicPacket = BuildWolPacket(macBytes);

        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;
        
        await udpClient.SendAsync(magicPacket, magicPacket.Length, new IPEndPoint(IPAddress.Broadcast, 9));
    }

    /// <summary>
    /// Parses a MAC address string into a byte array.
    /// </summary>
    /// <param name="macAddress">MAC address in any standard format</param>
    /// <returns>6-byte MAC address</returns>
    private static byte[] ParseMacAddress(string macAddress)
    {
        var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(".", "");
        if (cleanMac.Length != 12)
            throw new ArgumentException("Invalid MAC address format");

        return Enumerable.Range(0, 6)
            .Select(i => Convert.ToByte(cleanMac.Substring(i * 2, 2), 16))
            .ToArray();
    }

    /// <summary>
    /// Builds a Wake-on-LAN magic packet from a MAC address.
    /// </summary>
    /// <param name="macBytes">6-byte MAC address</param>
    /// <returns>102-byte magic packet (6 bytes of 0xFF + 16 repetitions of MAC address)</returns>
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
