using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Utilities;
using SMBLibrary;
using SMBLibrary.Client;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// SMB/CIFS protocol plugin for network shares.
/// Uses native Windows 'net use' on Windows, mount.cifs on Linux, or SMBLibrary as fallback.
/// </summary>
public class SmbPlugin : IProtocolPlugin
{
    private readonly Dictionary<string, string> _mountedPaths = new();
    private readonly Dictionary<string, string> _mountCredentials = new();
    private static readonly object _mountLock = new object();

    public string ProtocolName => "SMB";

    public bool SupportsWakeOnLan => true;

    public bool RequiresAuthentication => true;

    public Task<bool> TestConnection(Device device)
    {
        // Use SMBLibrary for cross-platform connection testing
        // Note: SMBLibrary Connect() doesn't support custom ports - it always uses 445
        // This limitation is documented in integration tests
        var client = new SMB2Client();
        try
        {
            var connected = client.Connect(device.Host, SMBTransportType.DirectTCPTransport);
            if (!connected)
            {
                return Task.FromResult(false);
            }

            var status = client.Login(string.Empty, device.Username, device.Password.GetPlaintext());
            if (status != NTStatus.STATUS_SUCCESS)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
        finally
        {
            client.Disconnect();
        }
    }

    public Task<string> MountShare(Device device, Share share)
    {
        var uncPath = BuildUncPath(device, share);
        var password = device.Password.GetPlaintext();

        lock (_mountLock)
        {
            // Check if already mounted
            if (_mountedPaths.ContainsKey(uncPath))
            {
                return Task.FromResult(_mountedPaths[uncPath]);
            }

            string mountPoint;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mountPoint = MountShareWindows(device, share, uncPath, password);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                mountPoint = MountShareLinux(device, share, uncPath, password);
            }
            else
            {
                throw new PlatformNotSupportedException($"SMB mounting not supported on {RuntimeInformation.OSDescription}");
            }

            _mountedPaths[uncPath] = mountPoint;
            _mountCredentials[uncPath] = $"{device.Username}:{password}";
            return Task.FromResult(mountPoint);
        }
    }

    private string MountShareWindows(Device device, Share share, string uncPath, string password)
    {
        // Find an available drive letter
        var driveLetter = FindAvailableDriveLetter();
        if (driveLetter == null)
        {
            throw new InvalidOperationException("No available drive letters for SMB mount");
        }

        var mountPoint = $"{driveLetter}:";

        // Mount using net use
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"use {mountPoint} \"{uncPath}\" /user:{device.Username} \"{password}\" /persistent:no",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to mount SMB share '{uncPath}'. Exit code: {process.ExitCode}. Error: {error}");
            }

            return mountPoint;
        }
        finally
        {
            process.Dispose();
        }
    }

    private string MountShareLinux(Device device, Share share, string uncPath, string password)
    {
        // Create a temporary mount point
        var mountPoint = Path.Combine("/tmp", $"backupchrono-smb-{Guid.NewGuid()}");
        Directory.CreateDirectory(mountPoint);

        // Create credentials file for security
        var credFile = Path.Combine("/tmp", $".smbcreds-{Guid.NewGuid()}");
        File.WriteAllText(credFile, $"username={device.Username}\npassword={password}\n");

        try
        {
            // Set restrictive permissions on credential file
            var chmodProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"600 {credFile}",
                RedirectStandardError = true,
                UseShellExecute = false
            });
            chmodProcess?.WaitForExit();

            // Mount using mount.cifs with port support
            var portOption = device.Port.HasValue && device.Port.Value != 445 
                ? $",port={device.Port.Value}" 
                : "";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mount",
                    Arguments = $"-t cifs \"{uncPath}\" \"{mountPoint}\" -o credentials={credFile},vers=3.0,noperm{portOption}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                // Cleanup on failure
                try { Directory.Delete(mountPoint); } catch { }
                
                throw new InvalidOperationException(
                    $"Failed to mount SMB share '{uncPath}'. Exit code: {process.ExitCode}. Error: {error}. " +
                    $"Make sure cifs-utils is installed: apt-get install cifs-utils");
            }

            return mountPoint;
        }
        finally
        {
            // Clean up credentials file
            try { File.Delete(credFile); } catch { }
        }
    }

    public async Task UnmountShare(string mountPath)
    {
        lock (_mountLock)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UnmountShareWindows(mountPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                UnmountShareLinux(mountPath);
            }

            // Remove from tracking (even if unmount failed, don't track it)
            var entry = _mountedPaths.FirstOrDefault(x => x.Value == mountPath);
            if (entry.Key != null)
            {
                _mountedPaths.Remove(entry.Key);
                _mountCredentials.Remove(entry.Key);
            }
        }

        await Task.CompletedTask;
    }

    private void UnmountShareWindows(string mountPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"use {mountPath} /delete /yes",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"Failed to unmount SMB share at '{mountPath}'. Exit code: {process.ExitCode}. Error: {error}");
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private void UnmountShareLinux(string mountPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "umount",
                Arguments = mountPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"Failed to unmount SMB share at '{mountPath}'. Exit code: {process.ExitCode}. Error: {error}");
            }

            // Clean up the mount point directory
            try
            {
                if (Directory.Exists(mountPath))
                {
                    Directory.Delete(mountPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    public async Task WakeDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.WakeOnLanMacAddress))
        {
            throw new InvalidOperationException("Device does not have Wake-on-LAN MAC address configured");
        }

        await WakeOnLanHelper.SendMagicPacket(device.WakeOnLanMacAddress);
    }

    private string BuildUncPath(Device device, Share? share)
    {
        var host = device.Host;
        var port = device.Port ?? 445;
        
        // UNC path format: \\host\sharepath
        if (share == null)
        {
            return $"\\\\{host}";
        }

        // Clean up the share path - remove leading/trailing slashes and backslashes
        var sharePath = share.Path.Trim('\\', '/');
        
        // Build UNC path
        return $"\\\\{host}\\{sharePath}";
    }

    private char? FindAvailableDriveLetter()
    {
        var usedDrives = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        
        // Start from Z and work backwards to avoid conflicts with common drives
        for (char letter = 'Z'; letter >= 'D'; letter--)
        {
            if (!usedDrives.Contains(letter))
            {
                return letter;
            }
        }

        return null;
    }
}
