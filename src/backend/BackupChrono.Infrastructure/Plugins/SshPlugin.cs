using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// SSH/SFTP protocol plugin using Restic's native SFTP backend.
/// Supports password and SSH key authentication.
/// </summary>
public class SshPlugin : IProtocolPlugin
{
    private readonly ILogger<SshPlugin> _logger;
    private const int DefaultSshPort = 22;
    private const int ConnectionTimeoutSeconds = 30;

    public string ProtocolName => "SSH";
    public bool SupportsWakeOnLan => false; // SSH servers are typically always on
    public bool RequiresAuthentication => true;

    public SshPlugin(ILogger<SshPlugin> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tests SSH connectivity to the device.
    /// </summary>
    public async Task<bool> TestConnection(Device device)
    {
        _logger.LogInformation("Testing SSH connection to {Host}:{Port}", device.Host, GetPort(device));

        try
        {
            using var client = CreateSshClient(device);
            await Task.Run(() => client.Connect());

            if (!client.IsConnected)
            {
                _logger.LogWarning("SSH connection failed: Client not connected to {Host}", device.Host);
                return false;
            }

            _logger.LogInformation("SSH connection successful to {Host}", device.Host);
            client.Disconnect();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH connection test failed for {Host}:{Port}", device.Host, GetPort(device));
            return false;
        }
    }

    /// <summary>
    /// Returns the SFTP URI for Restic to use directly.
    /// Format: sftp:user@host[:port]:/path
    /// </summary>
    public Task<string> MountShare(Device device, Share share)
    {
        _logger.LogInformation("Preparing SFTP URI for device {Device}, share {Share}", device.Name, share.Name);

        var port = GetPort(device);
        var path = NormalizePath(share.Path);
        
        // Build SFTP URI for Restic
        // Format: sftp:username@host[:port]:/absolute/path
        var sftpUri = port == DefaultSshPort
            ? $"sftp:{device.Username}@{device.Host}:{path}"
            : $"sftp:{device.Username}@{device.Host}:{port}:{path}";

        _logger.LogInformation("SFTP URI prepared: {Uri}", MaskCredentials(sftpUri));
        return Task.FromResult(sftpUri);
    }

    /// <summary>
    /// No cleanup needed for SFTP URIs - Restic handles the connection directly.
    /// </summary>
    public Task UnmountShare(string mountPath)
    {
        _logger.LogDebug("UnmountShare called for SFTP URI (no-op): {MountPath}", MaskCredentials(mountPath));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Wake-on-LAN is not supported for SSH devices.
    /// </summary>
    public async Task WakeDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.WakeOnLanMacAddress))
        {
            throw new InvalidOperationException("Device does not have Wake-on-LAN MAC address configured");
        }

        // SSH servers are typically always on, but support WoL if requested
        _logger.LogInformation("Sending Wake-on-LAN packet to {Device}", device.Name);
        await WakeOnLanHelper.SendMagicPacket(device.WakeOnLanMacAddress);
    }

    /// <summary>
    /// Creates an SSH.NET client with appropriate authentication method.
    /// </summary>
    private SshClient CreateSshClient(Device device)
    {
        var port = GetPort(device);
        var password = device.Password.GetPlaintext();

        // Check if password looks like a private key (starts with -----BEGIN)
        if (password.TrimStart().StartsWith("-----BEGIN"))
        {
            _logger.LogDebug("Using SSH key authentication for {Host}", device.Host);
            
            using var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(password));
            var keyFile = new PrivateKeyFile(keyStream);
            var authMethod = new PrivateKeyAuthenticationMethod(device.Username, keyFile);
            
            var connectionInfo = new ConnectionInfo(device.Host, port, device.Username, authMethod)
            {
                Timeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds)
            };
            
            return new SshClient(connectionInfo);
        }
        else
        {
            _logger.LogDebug("Using password authentication for {Host}", device.Host);
            
            var authMethod = new PasswordAuthenticationMethod(device.Username, password);
            var connectionInfo = new ConnectionInfo(device.Host, port, device.Username, authMethod)
            {
                Timeout = TimeSpan.FromSeconds(ConnectionTimeoutSeconds)
            };
            
            return new SshClient(connectionInfo);
        }
    }

    /// <summary>
    /// Gets the SSH port, defaulting to 22 if not specified.
    /// </summary>
    private int GetPort(Device device) => device.Port ?? DefaultSshPort;

    /// <summary>
    /// Normalizes the share path to absolute Unix format.
    /// </summary>
    private string NormalizePath(string path)
    {
        // Ensure path starts with / for absolute paths
        if (!path.StartsWith('/'))
        {
            path = '/' + path;
        }

        // Remove trailing slash except for root
        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        return path;
    }

    /// <summary>
    /// Masks sensitive information in SFTP URIs for logging.
    /// </summary>
    private string MaskCredentials(string uri)
    {
        // Hide username in logs: sftp:user@host -> sftp:***@host
        if (uri.Contains("sftp:") && uri.Contains('@'))
        {
            var parts = uri.Split('@', 2);
            return $"sftp:***@{parts[1]}";
        }
        return uri;
    }
}
